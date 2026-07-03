using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// One-click setup: builds a walkable scene from the imported city FBX.
/// Menu: St. John's -> Build Walkable Scene
public static class StJohnsSceneBuilder
{
    const string FbxPath = "Assets/City/Tiles/downtown.fbx";
    const string MatDir = "Assets/City/Materials";
    const string ScenePath = "Assets/Scenes/CityWalk.unity";

    [MenuItem("St. John's/Build Walkable Scene")]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("St. John's", "Please exit Play Mode before building the scene.", "OK");
            return;
        }

        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (fbx == null)
        {
            EditorUtility.DisplayDialog("St. John's", $"City FBX not found at {FbxPath}. Let Unity finish importing first.", "OK");
            return;
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // --- city ---
        var city = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        city.name = "City_Downtown";

        if (!AssetDatabase.IsValidFolder(MatDir))
        {
            AssetDatabase.CreateFolder("Assets/City", "Materials");
        }
        var vcol = MakeMaterial("VertexColorCity", VertexColorShader(), Color.white, 0.9f);
        var asphalt = MakeMaterial("Asphalt", LitShader(), new Color(0.16f, 0.16f, 0.17f), 0.9f);
        var path = MakeMaterial("Path", LitShader(), new Color(0.55f, 0.5f, 0.44f), 1f);
        var water = MakeMaterial("Water", LitShader(), new Color(0.09f, 0.22f, 0.30f), 0.1f);

        foreach (var mr in city.GetComponentsInChildren<MeshRenderer>())
        {
            string n = mr.gameObject.name;
            if (n.StartsWith("Roads"))
            {
                mr.sharedMaterial = asphalt;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.transform.position += Vector3.up * 0.08f; // Fix z-fighting
            }
            else if (n.StartsWith("Paths"))
            {
                mr.sharedMaterial = path;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.transform.position += Vector3.up * 0.08f; // Fix z-fighting
            }
            else if (n.StartsWith("Sea") || n.StartsWith("Lakes"))
            {
                mr.sharedMaterial = water;
                mr.shadowCastingMode = ShadowCastingMode.Off;
            }
            else
            {
                mr.sharedMaterial = vcol;   // Terrain, Buildings
            }

            // Don't add colliders to water
            if (!n.StartsWith("Sea") && !n.StartsWith("Lakes"))
            {
                mr.gameObject.AddComponent<MeshCollider>();
            }

            GameObjectUtility.SetStaticEditorFlags(mr.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
        }

        // --- light, sky mood ---
        var sunGO = new GameObject("Sun");
        var sun = sunGO.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.3f;
        sun.shadows = LightShadows.Soft;
        sun.color = new Color(1f, 0.96f, 0.88f);
        sunGO.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.65f, 0.72f, 0.80f);
        RenderSettings.fogStartDistance = 600f;
        RenderSettings.fogEndDistance = 7000f;

        if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            urp.shadowDistance = 900f;

        // --- player, spawned on solid ground near the middle of town ---
        Physics.SyncTransforms();
        Bounds b = default;
        foreach (var mr in city.GetComponentsInChildren<MeshRenderer>())
            if (mr.gameObject.name.StartsWith("Buildings")) b = mr.bounds;
        Vector3 spawnTop = b.center + Vector3.up * 1500f;
        Vector3 spawnPos = b.center + Vector3.up * 60f;
        if (Physics.Raycast(spawnTop, Vector3.down, out var hit, 4000f))
            spawnPos = hit.point + Vector3.up * 1.5f;

        var player = new GameObject("Player");
        player.transform.position = spawnPos;
        var cc = player.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.35f;
        cc.center = new Vector3(0, 0.9f, 0);

        var camGO = new GameObject("PlayerCamera");
        camGO.transform.SetParent(player.transform, false);
        camGO.transform.localPosition = new Vector3(0, 1.65f, 0);
        var cam = camGO.AddComponent<Camera>();
        cam.farClipPlane = 25000f;
        cam.nearClipPlane = 0.3f;
        camGO.AddComponent<AudioListener>();
        camGO.tag = "MainCamera";

        var walker = player.AddComponent<SimpleWalker>();
        walker.cameraPivot = camGO.transform;

        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            AssetDatabase.CreateFolder("Assets", "Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        Selection.activeGameObject = player;
        EditorUtility.DisplayDialog("St. John's",
            "Scene built and saved (Assets/Scenes/CityWalk.unity).\n\nPress Play to walk around.\nWASD move, mouse look, Shift sprint, Space jump, Esc frees the mouse.", "Let's go");
    }

    static Shader VertexColorShader()
    {
        var s = Shader.Find("StJohns/VertexColorLit");
        return s != null ? s : LitShader();
    }

    static Shader LitShader() => Shader.Find("Universal Render Pipeline/Lit");

    static Material MakeMaterial(string name, Shader shader, Color color, float roughness)
    {
        string assetPath = $"{MatDir}/{name}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        if (existing != null) return existing;
        var m = new Material(shader);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 1f - roughness);
        AssetDatabase.CreateAsset(m, assetPath);
        return m;
    }
}
