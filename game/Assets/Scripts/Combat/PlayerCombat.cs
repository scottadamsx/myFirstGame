using UnityEngine;
using UnityEngine.InputSystem;

/// Fists and the pistol. LMB attacks (cursor locked), Q swaps weapons once
/// the pistol is owned. Gunfire panics civilians.
public class PlayerCombat : MonoBehaviour
{
    public bool pistolOwned;
    public bool pistolEquipped;
    public int ammo;

    float nextAttack;
    GameManager gm;

    void Start()
    {
        gm = GameManager.Instance;
    }

    void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        if (kb.qKey.wasPressedThisFrame && pistolOwned)
        {
            pistolEquipped = !pistolEquipped;
            GameHUD.Toast(pistolEquipped ? "Pistol out." : "Fists up.");
        }

        if (Cursor.lockState != CursorLockMode.Locked) return;
        if (mouse.leftButton.wasPressedThisFrame && Time.time >= nextAttack)
        {
            if (pistolEquipped && pistolOwned) Shoot();
            else Punch();
        }
    }

    Vector3 CamForward()
    {
        var cam = Camera.main;
        return cam != null ? cam.transform.forward : transform.forward;
    }

    void Punch()
    {
        nextAttack = Time.time + 0.5f;
        Vector3 fwd = CamForward();
        fwd.y = 0;
        fwd.Normalize();
        Vector3 origin = transform.position + Vector3.up * 1.2f + fwd * 1.3f;
        foreach (var hit in Physics.OverlapSphere(origin, 1.05f))
        {
            var h = hit.GetComponentInParent<Health>();
            if (h != null && h.gameObject != gameObject)
            {
                h.Damage(28f, transform.position);
                var ai = h.GetComponent<EnemyAI>();
                if (ai != null) ai.Stagger(fwd);
            }
        }
        PedestrianSystem.PanicAt(transform.position, 10f);
    }

    void Shoot()
    {
        if (ammo <= 0)
        {
            GameHUD.Toast("Click — out of rounds. Wince at the Battery sells 'em.");
            nextAttack = Time.time + 0.4f;
            return;
        }
        nextAttack = Time.time + 0.35f;
        ammo--;

        var cam = Camera.main;
        if (cam == null) return;
        Vector3 dir = cam.transform.forward;
        Vector3 start = cam.transform.position + dir * 2f;
        Vector3 end = start + dir * 130f;
        if (Physics.Raycast(start, dir, out var hit, 130f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            end = hit.point;
            var h = hit.collider.GetComponentInParent<Health>();
            if (h != null)
            {
                h.Damage(40f, transform.position);
                var ai = h.GetComponentInParent<EnemyAI>();
                if (ai != null) ai.Stagger(dir);
            }
        }
        Tracer(start + Vector3.down * 0.2f, end);
        PedestrianSystem.PanicAt(transform.position, 45f);
    }

    static void Tracer(Vector3 a, Vector3 b)
    {
        var go = new GameObject("Tracer");
        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.SetPosition(0, a);
        lr.SetPosition(1, b);
        lr.startWidth = 0.03f;
        lr.endWidth = 0.015f;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = new Color(1f, 0.9f, 0.5f, 0.9f);
        lr.endColor = new Color(1f, 0.9f, 0.5f, 0.15f);
        Object.Destroy(go, 0.07f);
    }
}
