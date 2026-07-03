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
    }

    /// Builds a simple car: body, cabin, four wheels, one box collider.
    public static GameObject BuildVisual(Color color, string name)
    {
        var root = new GameObject(name);

        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.Destroy(body.GetComponent<Collider>());
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0, 0.55f, 0);
        body.transform.localScale = new Vector3(1.8f, 0.55f, 4.2f);
        Tint(body, color);

        var cabin = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.Destroy(cabin.GetComponent<Collider>());
        cabin.transform.SetParent(root.transform, false);
        cabin.transform.localPosition = new Vector3(0, 1.05f, -0.35f);
        cabin.transform.localScale = new Vector3(1.6f, 0.5f, 2.0f);
        Tint(cabin, Color.Lerp(color, new Color(0.1f, 0.12f, 0.15f), 0.55f));

        for (int i = 0; i < 4; i++)
        {
            var w = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.Destroy(w.GetComponent<Collider>());
            w.transform.SetParent(root.transform, false);
            w.transform.localRotation = Quaternion.Euler(0, 0, 90);
            w.transform.localScale = new Vector3(0.62f, 0.14f, 0.62f);
            w.transform.localPosition = new Vector3(i % 2 == 0 ? -0.85f : 0.85f, 0.31f, i < 2 ? 1.35f : -1.35f);
            Tint(w, new Color(0.08f, 0.08f, 0.09f));
        }

        var col = root.AddComponent<BoxCollider>();
        col.center = new Vector3(0, 0.7f, 0);
        col.size = new Vector3(1.85f, 1.15f, 4.25f);
        return root;
    }

    static void Tint(GameObject go, Color c)
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", c);
        mat.SetFloat("_Smoothness", 0.55f);
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
