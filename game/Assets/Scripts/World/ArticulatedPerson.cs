using UnityEngine;

/// Procedural articulated human: head with a face, torso, jointed arms and
/// legs, optional toque. Self-animates — walk cycle driven by actual movement
/// speed (measured from position deltas), gentle breathing when idle.
public class ArticulatedPerson : MonoBehaviour
{
    Transform torso, headJoint;
    Transform armL, armR, forearmL, forearmR;
    Transform thighL, thighR, shinL, shinR;
    float torsoBaseY;
    float phase;
    Vector3 lastPos;

    static Shader litShader;
    static Texture2D faceAtlas;

    void Start()
    {
        lastPos = transform.position;
    }

    void Update()
    {
        Vector3 p = transform.position;
        Vector3 delta = p - lastPos;
        delta.y = 0;
        float speed = delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
        lastPos = p;
        Animate(speed);
    }

    void Animate(float speed)
    {
        bool moving = speed > 0.15f;
        float amp = moving ? Mathf.Clamp01(speed / 1.6f) : 0f;
        if (moving)
            phase += Time.deltaTime * Mathf.Clamp(speed, 0.6f, 4f) * 3.4f;

        float s = Mathf.Sin(phase);
        float legSwing = s * 34f * amp;

        thighL.localRotation = Quaternion.Euler(legSwing, 0, 0);
        thighR.localRotation = Quaternion.Euler(-legSwing, 0, 0);
        shinL.localRotation = Quaternion.Euler(Mathf.Max(0, -s) * 50f * amp, 0, 0);
        shinR.localRotation = Quaternion.Euler(Mathf.Max(0, s) * 50f * amp, 0, 0);

        armL.localRotation = Quaternion.Euler(-legSwing * 0.8f, 0, 7f);
        armR.localRotation = Quaternion.Euler(legSwing * 0.8f, 0, -7f);
        forearmL.localRotation = Quaternion.Euler(-16f - Mathf.Max(0, -s) * 22f * amp, 0, 0);
        forearmR.localRotation = Quaternion.Euler(-16f - Mathf.Max(0, s) * 22f * amp, 0, 0);

        float breath = Mathf.Sin(Time.time * 1.6f + phase) * 1.4f;
        torso.localRotation = Quaternion.Euler(4f * amp + breath * (moving ? 0.3f : 1f), 0, 0);
        torso.localPosition = new Vector3(0, torsoBaseY + Mathf.Abs(Mathf.Cos(phase)) * 0.035f * amp, 0);
        headJoint.localRotation = Quaternion.Euler(-4f * amp - breath * 0.4f, 0, 0);
    }

    // ---------------- construction ----------------

    public static ArticulatedPerson Build(Color coat, Color pants, Color skin, Color hair,
                                          int faceVariant, bool toque, Color toqueColor, float scale = 1f)
    {
        if (litShader == null) litShader = Shader.Find("Universal Render Pipeline/Lit");
        if (faceAtlas == null) faceAtlas = Resources.Load<Texture2D>("City/faces");

        var root = new GameObject("Person");
        var p = root.AddComponent<ArticulatedPerson>();

        var hips = new GameObject("Hips").transform;
        hips.SetParent(root.transform, false);
        hips.localPosition = new Vector3(0, 0.92f, 0);
        Part(hips, PrimitiveType.Cube, new Vector3(0, 0, 0), new Vector3(0.32f, 0.18f, 0.20f), pants);

        p.torso = new GameObject("Torso").transform;
        p.torso.SetParent(hips, false);
        p.torso.localPosition = new Vector3(0, 0.09f, 0);
        p.torsoBaseY = 0.09f;
        Part(p.torso, PrimitiveType.Capsule, new Vector3(0, 0.28f, 0), new Vector3(0.36f, 0.29f, 0.26f), coat);

        p.headJoint = new GameObject("Head").transform;
        p.headJoint.SetParent(p.torso, false);
        p.headJoint.localPosition = new Vector3(0, 0.62f, 0);
        Part(p.headJoint, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.27f, skin);
        if (toque)
        {
            Part(p.headJoint, PrimitiveType.Cylinder, new Vector3(0, 0.115f, -0.01f), new Vector3(0.26f, 0.055f, 0.26f), toqueColor);
            Part(p.headJoint, PrimitiveType.Sphere, new Vector3(0, 0.17f, -0.01f), Vector3.one * 0.09f, toqueColor);
        }
        else
        {
            Part(p.headJoint, PrimitiveType.Sphere, new Vector3(0, 0.06f, -0.035f), new Vector3(0.275f, 0.22f, 0.26f), hair);
        }
        AddFace(p.headJoint, faceVariant);

        p.armL = Limb(hips, new Vector3(-0.245f, 0.50f, 0), 0.29f, 0.062f, coat, out p.forearmL, skin);
        p.armR = Limb(hips, new Vector3(0.245f, 0.50f, 0), 0.29f, 0.062f, coat, out p.forearmR, skin);
        p.thighL = Limb(hips, new Vector3(-0.10f, -0.05f, 0), 0.42f, 0.078f, pants, out p.shinL, pants, foot: true);
        p.thighR = Limb(hips, new Vector3(0.10f, -0.05f, 0), 0.42f, 0.078f, pants, out p.shinR, pants, foot: true);

        root.transform.localScale = Vector3.one * scale;
        return p;
    }

    static Transform Limb(Transform parent, Vector3 jointPos, float upperLen, float radius,
                           Color upperColor, out Transform lower, Color lowerColor, bool foot = false)
    {
        var upper = new GameObject("Upper").transform;
        upper.SetParent(parent, false);
        upper.localPosition = jointPos;
        Part(upper, PrimitiveType.Capsule, new Vector3(0, -upperLen / 2, 0),
             new Vector3(radius * 2, upperLen / 2 + radius, radius * 2), upperColor);

        lower = new GameObject("Lower").transform;
        lower.SetParent(upper, false);
        lower.localPosition = new Vector3(0, -upperLen - radius * 0.4f, 0);
        float lowLen = upperLen * 0.9f;
        Part(lower, PrimitiveType.Capsule, new Vector3(0, -lowLen / 2, 0),
             new Vector3(radius * 1.8f, lowLen / 2 + radius * 0.9f, radius * 1.8f), lowerColor);

        if (foot)
            Part(lower, PrimitiveType.Cube, new Vector3(0, -lowLen - 0.02f, 0.05f), new Vector3(0.11f, 0.07f, 0.24f),
                 new Color(0.15f, 0.13f, 0.12f));
        else
            Part(lower, PrimitiveType.Sphere, new Vector3(0, -lowLen - 0.02f, 0), Vector3.one * radius * 2.2f, lowerColor);

        return upper;
    }

    static void Part(Transform parent, PrimitiveType type, Vector3 localPos, Vector3 localScale, Color color)
    {
        var go = GameObject.CreatePrimitive(type);
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;
        var mat = new Material(litShader);
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", 0.25f);
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    static void AddFace(Transform head, int variant)
    {
        if (faceAtlas == null) return;
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.Destroy(quad.GetComponent<Collider>());
        quad.transform.SetParent(head, false);
        quad.transform.localPosition = new Vector3(0, -0.005f, 0.125f);
        quad.transform.localScale = Vector3.one * 0.235f;
        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.mainTexture = faceAtlas;
        mat.mainTextureScale = new Vector2(0.5f, 0.5f);
        // atlas: 0 top-left, 1 top-right, 2 bottom-left, 3 bottom-right (UV origin bottom-left)
        Vector2[] offsets = { new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(0.5f, 0) };
        mat.mainTextureOffset = offsets[Mathf.Abs(variant) % 4];
        quad.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }
}
