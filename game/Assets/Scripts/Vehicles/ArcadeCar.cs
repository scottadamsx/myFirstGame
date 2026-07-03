using UnityEngine;
using UnityEngine.InputSystem;

/// Arcade car physics: forgiving, grippy, fun. No wheel colliders — a
/// rigidbody box with raycast ground check and lateral-slip damping.
[RequireComponent(typeof(Rigidbody))]
public class ArcadeCar : MonoBehaviour
{
    public bool playerControlled;
    public float accelForce = 11000f;
    public float maxSpeed = 27f;       // ~100 km/h
    public float steerRate = 95f;      // deg/sec at full lock
    public float grip = 8f;            // lateral slip damping

    Rigidbody rb;
    Transform visuals;
    Vector3 lastVel;
    float pitch, roll;
    TrailRenderer[] skidmarks;

    public static readonly Color[] Palette =
    {
        new Color(0.72f, 0.21f, 0.17f), new Color(0.24f, 0.44f, 0.66f),
        new Color(0.25f, 0.48f, 0.28f), new Color(0.91f, 0.77f, 0.24f),
        new Color(0.86f, 0.50f, 0.20f), new Color(0.18f, 0.55f, 0.53f),
        new Color(0.90f, 0.90f, 0.92f), new Color(0.15f, 0.15f, 0.17f),
        new Color(0.55f, 0.56f, 0.60f), new Color(0.43f, 0.30f, 0.52f),
    };

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.mass = 1200f;
        rb.linearDamping = 0.08f;
        rb.angularDamping = 4f;
        rb.centerOfMass = new Vector3(0, -0.45f, 0);
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;  // valid for kinematic parked cars too

        // near-frictionless body: grip is faked in code, and real friction was
        // pinning the car against ground/walls harder than the engine could push
        var slippery = new PhysicsMaterial("CarBody")
        {
            dynamicFriction = 0.1f,
            staticFriction = 0.1f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounciness = 0f,
            bounceCombine = PhysicsMaterialCombine.Minimum,
        };
        var box = GetComponent<BoxCollider>();
        if (box != null) box.sharedMaterial = slippery;
    }

    void Start()
    {
        visuals = transform.Find("Visuals");
        skidmarks = GetComponentsInChildren<TrailRenderer>();
    }

    public bool Grounded =>
        Physics.Raycast(transform.position + Vector3.up * 0.3f, Vector3.down, 0.85f);

    float dbgTimer;

    void FixedUpdate()
    {
        if (!playerControlled) return;
        var kb = Keyboard.current;
        if (kb == null)
        {
            Debug.Log("DRIVE-DBG: Keyboard.current is NULL while driving");
            return;
        }

        float throttle = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1 : 0)
                       - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1 : 0);
        float steer = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1 : 0)
                    - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1 : 0);
        bool handbrake = kb.spaceKey.isPressed;

        bool grounded = Grounded;
        dbgTimer += Time.fixedDeltaTime;
        if (dbgTimer > 1.5f)
        {
            dbgTimer = 0;
            Debug.Log($"DRIVE-DBG state: throttle={throttle} steer={steer} grounded={grounded} " +
                      $"kinematic={rb.isKinematic} enabled={enabled} speed={rb.linearVelocity.magnitude:F1} " +
                      $"pos={transform.position} timeScale={Time.timeScale}");
        }

        if (!grounded) return;

        Vector3 vel = rb.linearVelocity;
        Vector3 flatVel = new Vector3(vel.x, 0, vel.z);
        float forwardSpeed = Vector3.Dot(flatVel, transform.forward);

        if (Mathf.Abs(forwardSpeed) < maxSpeed)
            rb.AddForce(transform.forward * throttle * accelForce);

        // rolling resistance so a frictionless car still coasts to a stop
        if (Mathf.Approximately(throttle, 0f))
            rb.AddForce(-flatVel * rb.mass * 1.1f * Time.fixedDeltaTime, ForceMode.Impulse);

        // steering scales with speed so the car doesn't spin on the spot
        float steerScale = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / 6f) * Mathf.Sign(forwardSpeed == 0 ? 1 : forwardSpeed);
        Quaternion turn = Quaternion.Euler(0, steer * steerRate * steerScale * Time.fixedDeltaTime, 0);
        rb.MoveRotation(rb.rotation * turn);

        // lateral grip (reduced while handbraking = slides)
        float g = handbrake ? grip * 0.15f : grip;
        Vector3 side = transform.right * Vector3.Dot(flatVel, transform.right);
        rb.AddForce(-side * g * rb.mass * Time.fixedDeltaTime, ForceMode.Impulse);

        if (handbrake)
            rb.AddForce(-flatVel.normalized * rb.mass * 6f * Time.fixedDeltaTime, ForceMode.Impulse);

        Vector3 accel = (rb.linearVelocity - lastVel) / Time.fixedDeltaTime;
        lastVel = rb.linearVelocity;
        Vector3 locAccel = transform.InverseTransformDirection(accel);
        
        float targetPitch = Mathf.Clamp(-locAccel.z * 0.5f, -6f, 6f);
        float targetRoll = Mathf.Clamp(locAccel.x * 0.6f, -10f, 10f);
        pitch = Mathf.Lerp(pitch, targetPitch, Time.fixedDeltaTime * 10f);
        roll = Mathf.Lerp(roll, targetRoll, Time.fixedDeltaTime * 10f);
        
        bool sliding = handbrake || Mathf.Abs(locAccel.x) > 8f;
        if (skidmarks != null)
        {
            foreach (var sm in skidmarks) sm.emitting = grounded && sliding && flatVel.magnitude > 4f;
        }
    }

    void Update()
    {
        if (visuals != null)
        {
            visuals.localRotation = Quaternion.Euler(pitch, 0, roll);
        }
    }

    /// Builds a car that reads as a car: low body, glass cabin band, roof,
    /// proper wheels with hubs, emissive head/tail lights. One box collider.
    public static GameObject BuildVisual(Color color, string name)
    {
        var root = new GameObject(name);
        var visuals = new GameObject("Visuals");
        visuals.transform.SetParent(root.transform, false);

        Color glass = new Color(0.09f, 0.11f, 0.14f);

        Part(visuals, new Vector3(0, 0.48f, 0), new Vector3(1.76f, 0.44f, 4.1f), color, 0.6f);          // body
        Part(visuals, new Vector3(0, 0.92f, -0.25f), new Vector3(1.58f, 0.4f, 2.05f), glass, 0.9f);     // glass band
        Part(visuals, new Vector3(0, 1.14f, -0.25f), new Vector3(1.5f, 0.07f, 1.9f), color, 0.6f);      // roof
        Part(visuals, new Vector3(0, 0.62f, 2.02f), new Vector3(1.7f, 0.16f, 0.1f), Color.Lerp(color, Color.black, 0.35f), 0.4f);  // bumper hint

        for (int s = -1; s <= 1; s += 2)
        {
            Emissive(visuals, new Vector3(s * 0.56f, 0.56f, 2.06f), new Vector3(0.3f, 0.12f, 0.05f), new Color(1f, 0.95f, 0.8f));   // headlights
            Emissive(visuals, new Vector3(s * 0.6f, 0.56f, -2.06f), new Vector3(0.26f, 0.12f, 0.05f), new Color(0.9f, 0.1f, 0.08f)); // taillights
        }

        for (int i = 0; i < 4; i++)
        {
            float x = i % 2 == 0 ? -0.86f : 0.86f;
            float z = i < 2 ? 1.32f : -1.32f;
            var w = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.Destroy(w.GetComponent<Collider>());
            w.transform.SetParent(visuals.transform, false);
            w.transform.localRotation = Quaternion.Euler(0, 0, 90);
            w.transform.localScale = new Vector3(0.68f, 0.15f, 0.68f);
            w.transform.localPosition = new Vector3(x, 0.34f, z);
            TintObj(w, new Color(0.07f, 0.07f, 0.08f), 0.3f);

            var hub = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.Destroy(hub.GetComponent<Collider>());
            hub.transform.SetParent(visuals.transform, false);
            hub.transform.localRotation = Quaternion.Euler(0, 0, 90);
            hub.transform.localScale = new Vector3(0.3f, 0.16f, 0.3f);
            hub.transform.localPosition = new Vector3(x, 0.34f, z);
            TintObj(hub, new Color(0.55f, 0.55f, 0.58f), 0.7f);

            // Skidmark trails on rear wheels
            if (i >= 2) 
            {
                var trail = new GameObject("Skidmark").AddComponent<TrailRenderer>();
                trail.transform.SetParent(root.transform, false);
                trail.transform.localPosition = new Vector3(x, 0.05f, z);
                trail.time = 2.5f;
                trail.startWidth = 0.4f;
                trail.endWidth = 0.4f;
                trail.minVertexDistance = 0.2f;
                trail.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                trail.material.SetColor("_BaseColor", new Color(0.1f, 0.1f, 0.1f, 0.8f));
                trail.material.SetFloat("_Surface", 1); // Transparent
                trail.material.renderQueue = 3000;
                trail.emitting = false;
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        var col = root.AddComponent<BoxCollider>();
        col.center = new Vector3(0, 0.68f, 0);
        col.size = new Vector3(1.82f, 1.1f, 4.15f);
        return root;
    }

    static void Part(GameObject root, Vector3 pos, Vector3 scale, Color c, float smooth)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(root.transform, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        TintObj(go, c, smooth);
    }

    static void Emissive(GameObject root, Vector3 pos, Vector3 scale, Color c)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(root.transform, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", c);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", c * 1.8f);
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    static void TintObj(GameObject go, Color c, float smooth)
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", c);
        mat.SetFloat("_Smoothness", smooth);
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    public static ArcadeCar SpawnParked(Vector3 pos, Quaternion rot, Color color)
    {
        var go = BuildVisual(color, "Car");
        go.transform.SetPositionAndRotation(pos, rot);
        var car = go.AddComponent<ArcadeCar>();   // adds Rigidbody via RequireComponent
        // kinematic until entered so parked cars don't roll down the hills
        car.GetComponent<Rigidbody>().isKinematic = true;
        return car;
    }
}
