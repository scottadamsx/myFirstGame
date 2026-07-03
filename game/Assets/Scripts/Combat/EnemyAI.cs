using UnityEngine;

/// A scrapper: chases the player on foot, swings when close, staggers when
/// hit, drops $10 when put down. Spawned by combat missions.
public class EnemyAI : MonoBehaviour
{
    GameManager gm;
    Health health;
    float nextSwing;
    float staggerUntil;
    bool dead;

    public bool Dead => dead;

    public static EnemyAI Spawn(Vector3 pos, int seed)
    {
        var prng = new System.Random(seed);
        var person = ArticulatedPerson.Build(
            new Color(0.25f, 0.25f, 0.28f),
            new Color(0.15f, 0.15f, 0.17f),
            new Color(0.8f, 0.62f, 0.48f),
            new Color(0.12f, 0.1f, 0.08f),
            2, prng.NextDouble() < 0.5, new Color(0.15f, 0.15f, 0.16f),
            0.98f + (float)prng.NextDouble() * 0.1f);
        var go = person.gameObject;
        go.name = "Scrapper";
        go.transform.position = pos;

        var col = go.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0, 0.95f, 0);
        col.height = 1.9f;
        col.radius = 0.34f;

        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        var h = go.AddComponent<Health>();
        h.max = h.current = 60f;

        return go.AddComponent<EnemyAI>();
    }

    void Start()
    {
        gm = GameManager.Instance;
        health = GetComponent<Health>();
        health.onDeath = Die;
    }

    void Update()
    {
        if (dead || gm == null || gm.Player == null) return;
        if (Time.time < staggerUntil) return;

        Vector3 to = gm.PlayerPosition() - transform.position;
        to.y = 0;
        float d = to.magnitude;
        if (d > 70f) return;

        if (d > 1.7f)
        {
            Vector3 step = to.normalized * 3.4f * Time.deltaTime;
            Vector3 np = transform.position + step;
            transform.position = CoordinateMapper.DropToGround(np + Vector3.up * 2f, 2f);
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(to.normalized), 8f * Time.deltaTime);
        }
        else if (Time.time >= nextSwing && gm.Player.gameObject.activeSelf)
        {
            nextSwing = Time.time + 1.15f;
            var ph = gm.Player.GetComponent<Health>();
            if (ph != null) ph.Damage(9f, transform.position);
        }
    }

    public void Stagger(Vector3 dir)
    {
        staggerUntil = Time.time + 0.45f;
        dir.y = 0;
        transform.position += dir.normalized * 0.6f;
    }

    void Die()
    {
        dead = true;
        var person = GetComponent<ArticulatedPerson>();
        if (person != null) person.enabled = false;
        var col = GetComponent<CapsuleCollider>();
        if (col != null) col.enabled = false;
        transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y, 82f);
        if (gm != null)
        {
            gm.Loonies += 10;
            GameHUD.Toast("Scrapper down. +$10");
        }
        Destroy(gameObject, 8f);
    }
}
