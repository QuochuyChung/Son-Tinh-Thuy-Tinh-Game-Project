using UnityEngine;

namespace SonTinhThuyTinh.Combat
{
    // A target to test attacks on: shows what each hit does (damage number, flash, knock-back, pull, slow), falls over when its health is
    // gone and stands up again a few seconds later. Needs a Health on the same object; collider and meshes are made by the builder.
    [RequireComponent(typeof(Health))]
    public class TrainingDummy : MonoBehaviour, IHitReceiver
    {
        [SerializeField] Renderer[] renderers;
        [SerializeField] float respawnDelay = 3f;
        [SerializeField] Color baseColor = new(0.78f, 0.62f, 0.38f);

        Health health;
        Vector3 home;
        Quaternion homeRotation;
        Vector3 velocity;
        float flash;
        float slowUntil;
        float lastHitAt = -10f;
        float diedAt = -10f;
        MaterialPropertyBlock block;

        void Awake()
        {
            health = GetComponent<Health>();
            home = transform.position;
            homeRotation = transform.rotation;
            block = new MaterialPropertyBlock();
            health.Died += _ => diedAt = Time.time;
        }

        public void OnHit(DamageInfo info, Vector3 impulse)
        {
            if (health.IsDead) return;
            velocity = impulse;
            flash = 1f;
            lastHitAt = Time.time;
            FloatingText.Spawn(transform.position + Vector3.up * 2.3f, Mathf.RoundToInt(info.Amount).ToString(), info.IsHeavy ? new Color(1f, 0.55f, 0.2f) : Color.white);
        }

        public void ApplySlow(float factor, float seconds) => slowUntil = Time.time + seconds;

        void Update()
        {
            float dt = Time.deltaTime;

            if (health.IsDead)
            {
                // topple, then stand up again
                transform.rotation = Quaternion.RotateTowards(transform.rotation, homeRotation * Quaternion.Euler(-85f, 0f, 0f), 220f * dt);
                if (Time.time - diedAt > respawnDelay)
                {
                    health.Revive();
                    transform.SetPositionAndRotation(home, homeRotation);
                    velocity = Vector3.zero;
                }
                return;
            }

            // knocked about, then eases back to its post
            velocity.y -= 22f * dt;
            transform.position += velocity * dt;
            if (transform.position.y < home.y)
            {
                Vector3 p = transform.position;
                p.y = home.y;
                transform.position = p;
                velocity.y = 0f;
                velocity.x = Mathf.MoveTowards(velocity.x, 0f, 10f * dt);
                velocity.z = Mathf.MoveTowards(velocity.z, 0f, 10f * dt);
            }
            if (Time.time - lastHitAt > 1.2f)
            {
                Vector3 toHome = home - transform.position;
                toHome.y = 0f;
                transform.position += Vector3.ClampMagnitude(toHome, 2.5f * dt);
            }
            Vector3 lean = new(velocity.z, 0f, -velocity.x);
            transform.rotation = Quaternion.Slerp(transform.rotation, homeRotation * Quaternion.Euler(Vector3.ClampMagnitude(lean * 3f, 28f)), 12f * dt);

            flash = Mathf.MoveTowards(flash, 0f, 4f * dt);
            Color target = Time.time < slowUntil ? new Color(0.45f, 0.7f, 1f) : baseColor;
            Color shown = Color.Lerp(target, Color.white, flash);
            if (renderers == null) return;
            block.SetColor("_BaseColor", shown);
            foreach (Renderer r in renderers) if (r != null) r.SetPropertyBlock(block);
        }
    }
}
