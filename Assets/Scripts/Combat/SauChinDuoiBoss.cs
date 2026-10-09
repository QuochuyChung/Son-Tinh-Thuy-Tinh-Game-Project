using System.Collections;
using System.Collections.Generic;
using SonTinhThuyTinh.Player;
using SonTinhThuyTinh.Quest;
using SonTinhThuyTinh.UI;
using UnityEngine;

namespace SonTinhThuyTinh.Combat
{
    // Sấu Chín Đuôi, the boss of the Thủy Tinh map (docs/task-sau-chin-duoi.md). It lies in the deep water beside the island with only its
    // back and nine tails showing; when the player steps onto the island it climbs out, roars and the boss bar appears.
    // Phase 1: walks / charges, bites, sweeps its tails round (red circle first) and spits water balls. Phase 2 (half health): roars again,
    // moves and recovers faster, spits three balls at once and adds the nine-tail slam (a big red circle on itself plus small ones under
    // the player). Winning gives the reward gift (banner + gift list); losing puts the player back at the edge of the island and the
    // crocodile back in the water with full health. All hits are checked by distance at the frame the clip lands (no hit colliders).
    // Clips (tools/rig_sau.py, AC_SauChinDuoi): Idle, Walk, Charge, Bite, TailSweep, WaterSpit, TailSlam, Hit, Roar, Death.
    [RequireComponent(typeof(Health))]
    public class SauChinDuoiBoss : MonoBehaviour, IHitReceiver
    {
        [Header("Parts")]
        [SerializeField] Animator animator;
        [SerializeField] Renderer[] renderers;
        [SerializeField] BossHealthBar bar;
        [SerializeField] string displayName = "Sấu Chín Đuôi";
        [Tooltip("Given when the crocodile dies (banner + gift list). Once the player has it, the crocodile is gone for good.")]
        [SerializeField] GiftItem reward;
        [SerializeField] Material warningMaterial;
        [SerializeField] Material spitMaterial;

        [Header("Arena")]
        [Tooltip("Centre of the island at ground level.")]
        [SerializeField] Transform arenaCenter;
        [Tooltip("Where it waits, in the water beside the island (only the back and the tails show).")]
        [SerializeField] Transform lair;
        [Tooltip("Where the player is put back after losing.")]
        [SerializeField] Transform playerRespawn;
        [SerializeField] float arenaRadius = 14.5f;
        [Tooltip("The fight starts when the player comes this close to the island's centre.")]
        [SerializeField] float wakeRadius = 13f;
        [Tooltip("Leaving the island by this much makes it go back into the water and heal.")]
        [SerializeField] float leashRadius = 26f;

        [Header("Movement")]
        [SerializeField] float walkSpeed = 2.4f;
        [SerializeField] float chargeSpeed = 9f;
        [Tooltip("Degrees per second.")]
        [SerializeField] float turnSpeed = 110f;

        [Header("Bite")]
        [SerializeField] float biteDamage = 16f;
        [Tooltip("Starts a bite when the player is this close (pivot to player).")]
        [SerializeField] float biteRange = 5f;
        [Tooltip("The jaws close this far in front of the pivot ...")]
        [SerializeField] float biteReach = 4f;
        [Tooltip("... and catch whatever is within this radius of that point.")]
        [SerializeField] float biteRadius = 2.3f;

        [Header("Tail sweep (red circle)")]
        [SerializeField] float sweepDamage = 20f;
        [SerializeField] float sweepRadius = 7f;

        [Header("Water spit")]
        [SerializeField] float spitDamage = 12f;
        [SerializeField] float spitSpeed = 16f;

        [Header("Nine-tail slam (phase 2)")]
        [SerializeField] float slamDamage = 28f;
        [SerializeField] float slamRadius = 8.5f;
        [SerializeField] float slamSmallRadius = 2.6f;

        [Header("Charge")]
        [SerializeField] float chargeDamage = 18f;

        [Tooltip("Phase 2 multiplies speeds by this and divides cooldowns by it.")]
        [SerializeField] float phase2Speedup = 1.3f;

        const float CrossFade = 0.15f;

        Health health;
        PlayerController player;
        bool awake, busy, phase2, phase2Pending, dead, resetting;
        float slowFactor = 1f, slowUntil;
        float flash;
        float poise;
        float groundY;
        float inWaterSince = -1f;
        string loopState;
        MaterialPropertyBlock block;
        readonly Dictionary<string, float> readyAt = new();
        readonly List<GameObject> warnings = new();

        public bool IsAwake => awake;
        public bool IsPhase2 => phase2;
        public Health Health => health;

        void Awake()
        {
            health = GetComponent<Health>();
            block = new MaterialPropertyBlock();
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        void Start()
        {
            groundY = arenaCenter != null ? arenaCenter.position.y : transform.position.y;
            if (bar != null) { bar.Bind(health, displayName); bar.Show(false); }
            if (reward != null && GiftTracker.Has(reward)) { gameObject.SetActive(false); return; }   // beaten on an earlier visit
            Sleep();
        }

        void OnDestroy()
        {
            if (health == null) return;
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }

        // ---------------------------------------------------------------- main loop

        float Speed => (phase2 ? phase2Speedup : 1f) * (Time.time < slowUntil ? slowFactor : 1f);

        void Update()
        {
            UpdateFlash();
            if (dead) return;
            FindPlayer();
            if (player == null) return;

            if (player.Health.IsDead)
            {
                if (awake && !resetting) { StopAllAttacks(); StartCoroutine(ResetAfterDefeat()); }
                return;
            }
            if (busy || resetting) return;

            Vector3 centre = arenaCenter.position;
            float fromCentre = Flat(player.transform.position - centre).magnitude;
            if (!awake)
            {
                if (fromCentre < wakeRadius) StartCoroutine(Wake());
                return;
            }
            if (fromCentre > leashRadius) { StartCoroutine(GiveUp()); return; }
            RescueFromWater(fromCentre);
            if (phase2Pending) { StartCoroutine(EnterPhase2()); return; }

            Vector3 toPlayer = Flat(player.transform.position - transform.position);
            float dist = toPlayer.magnitude;
            float angle = Vector3.Angle(transform.forward, toPlayer);

            if (phase2 && Ready("slam") && dist < 11f) { StartCoroutine(TailSlam()); return; }
            if (Ready("sweep") && dist < sweepRadius * 0.85f && (angle > 50f || dist < 3.5f)) { StartCoroutine(TailSweep()); return; }
            if (Ready("bite") && dist < biteRange && angle < 25f) { StartCoroutine(Bite()); return; }
            if (Ready("spit") && dist > 7.5f && angle < 15f) { StartCoroutine(WaterSpit()); return; }
            if (Ready("charge") && dist > 10f && angle < 10f) { StartCoroutine(Charge()); return; }

            // walk towards the player, turning first when the player is far to the side
            Turn(toPlayer, turnSpeed);
            if (dist > biteRange * 0.8f && angle < 60f)
            {
                Loop("Walk", Speed);
                Step(transform.forward * (walkSpeed * Speed * Time.deltaTime));
            }
            else Loop(angle > 20f ? "Walk" : "Idle", Speed);
        }

        // knocked (or walked) off the island into the deep water during the fight: back onto the island's rim after a moment
        void RescueFromWater(float fromCentre)
        {
            bool inWater = player.transform.position.y < groundY - 1.2f && fromCentre > arenaRadius - 1f;
            if (!inWater) { inWaterSince = -1f; return; }
            if (inWaterSince < 0f) { inWaterSince = Time.time; return; }
            if (Time.time - inWaterSince < 1.5f) return;
            Vector3 dir = Flat(player.transform.position - arenaCenter.position).normalized;
            player.SetBodyEnabled(false);
            player.transform.position = arenaCenter.position + dir * (arenaRadius - 1.5f) + Vector3.up * 0.3f;
            player.SetBodyEnabled(true);
            inWaterSince = -1f;
        }

        void FindPlayer()
        {
            if (player != null && player.isActiveAndEnabled) return;
            player = null;
            foreach (PlayerController candidate in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
                if (candidate.isActiveAndEnabled) { player = candidate; break; }
        }

        // ---------------------------------------------------------------- sleep, wake, phase 2, give up, reset

        void Sleep()
        {
            awake = false; busy = false; phase2 = false; phase2Pending = false; resetting = false;
            health.IsInvulnerable = true;
            readyAt.Clear();
            if (lair != null) transform.SetPositionAndRotation(lair.position, lair.rotation);
            Loop("Idle", 0.6f);
            if (bar != null) bar.Show(false);
        }

        IEnumerator Wake()
        {
            busy = true; awake = true;
            if (bar != null) bar.Show(true);
            // climb out of the water onto the island, facing the centre
            Vector3 from = transform.position;
            Vector3 dir = Flat(arenaCenter.position - from).normalized;
            Vector3 to = arenaCenter.position - dir * (arenaRadius * 0.45f);
            to.y = groundY;
            transform.rotation = Quaternion.LookRotation(dir);
            Loop("Walk", 1.2f);
            float time = Vector3.Distance(Flat(from), Flat(to)) / (walkSpeed * 1.4f);
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                float k = t / time;
                transform.position = Vector3.Lerp(from, to, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.3f);
                yield return null;
            }
            transform.position = to;
            Face(player.transform.position);
            yield return Roar();
            health.IsInvulnerable = false;
            Cooldown("bite", 0.5f); Cooldown("spit", 2.5f); Cooldown("sweep", 3f); Cooldown("charge", 4f);
            busy = false;
        }

        IEnumerator EnterPhase2()
        {
            busy = true; phase2Pending = false; phase2 = true;
            health.IsInvulnerable = true;
            if (bar != null) bar.SetPhase2(true);
            yield return Roar();
            health.IsInvulnerable = false;
            Cooldown("slam", 1f);
            busy = false;
        }

        IEnumerator Roar()
        {
            Play("Roar");
            yield return Wait(2f);
        }

        // the player ran off the island: back into the water, full health
        IEnumerator GiveUp()
        {
            busy = true;
            if (bar != null) bar.Show(false);
            yield return WalkTo(lair.position, walkSpeed * 1.5f);
            health.Revive();
            if (bar != null) bar.SetPhase2(false);
            Sleep();
        }

        IEnumerator ResetAfterDefeat()
        {
            resetting = true;
            Loop("Idle", 1f);
            yield return new WaitForSeconds(1.5f);
            Play("Roar");
            yield return new WaitForSeconds(2f);
            if (player != null)
            {
                Transform target = playerRespawn != null ? playerRespawn : arenaCenter;
                player.SetBodyEnabled(false);
                player.transform.SetPositionAndRotation(target.position, target.rotation);
                player.SetBodyEnabled(true);
                player.Revive();
            }
            health.Revive();
            if (bar != null) bar.SetPhase2(false);
            Sleep();
        }

        void StopAllAttacks()
        {
            StopAllCoroutines();
            ClearWarnings();
            busy = false;
        }

        // ---------------------------------------------------------------- attacks

        IEnumerator Bite()
        {
            busy = true;
            Play("Bite");
            float t = 0f;
            bool done = false;
            while (t < 1.2f)
            {
                float dt = Time.deltaTime * Speed;
                t += dt;
                if (t < 0.4f) Turn(Flat(player.transform.position - transform.position), turnSpeed * 0.8f);
                else if (t < 0.6f) Step(transform.forward * (6f * dt));                  // the lunge
                if (!done && t >= 0.55f)
                {
                    done = true;
                    Vector3 mouth = transform.position + transform.forward * biteReach;
                    if (InReach(mouth, biteRadius)) HitPlayer(biteDamage, false, mouth);
                }
                yield return null;
            }
            Cooldown("bite", 1.8f);
            busy = false;
        }

        IEnumerator TailSweep()
        {
            busy = true;
            Play("TailSweep");
            var w = Warn(transform.position, sweepRadius, 0.65f / Speed);
            float t = 0f;
            bool hit = false;
            while (t < 1.6f)
            {
                t += Time.deltaTime * Speed;
                if (w != null) w.transform.position = Ground(transform.position);
                if (!hit && t >= 0.65f && t <= 1.0f && InReach(transform.position, sweepRadius))
                {
                    hit = true;
                    HitPlayer(sweepDamage, true, transform.position);
                }
                if (t > 1.0f && w != null) { Unwarn(w); w = null; }
                yield return null;
            }
            Cooldown("sweep", 8f);
            busy = false;
        }

        IEnumerator WaterSpit()
        {
            busy = true;
            Play("WaterSpit");
            float t = 0f;
            bool spat = false;
            while (t < 1.4f)
            {
                t += Time.deltaTime * Speed;
                if (t < 0.5f) Turn(Flat(player.transform.position - transform.position), turnSpeed);
                if (!spat && t >= 0.6f)
                {
                    spat = true;
                    Vector3 mouth = transform.position + transform.forward * (biteReach * 0.85f) + Vector3.up * 1.4f;
                    Vector3 aim = player.transform.position + Vector3.up * 1.0f - mouth;
                    foreach (float yaw in phase2 ? new[] { -14f, 0f, 14f } : new[] { 0f })
                        SpawnSpit(mouth, Quaternion.Euler(0f, yaw, 0f) * aim.normalized);
                }
                yield return null;
            }
            Cooldown("spit", 5f);
            busy = false;
        }

        IEnumerator TailSlam()
        {
            busy = true;
            Play("TailSlam");
            float lead = 1.1f / Speed;
            var circles = new List<(Vector3 centre, float radius)> { (Ground(transform.position), slamRadius) };
            Vector3 p = Ground(player.transform.position);
            circles.Add((p, slamSmallRadius));
            for (int i = 0; i < 2; i++)
            {
                Vector2 r = Random.insideUnitCircle.normalized * Random.Range(3.5f, 6f);
                Vector3 c = p + new Vector3(r.x, 0f, r.y);
                Vector3 off = Flat(c - arenaCenter.position);
                if (off.magnitude > arenaRadius) c = arenaCenter.position + off.normalized * arenaRadius;
                circles.Add((Ground(c), slamSmallRadius));
            }
            var ws = new List<GameObject>();
            foreach (var (c, r) in circles) ws.Add(Warn(c, r, lead));
            float t = 0f;
            bool landed = false;
            while (t < 2.0f)
            {
                t += Time.deltaTime * Speed;
                if (!landed && t >= 1.1f)
                {
                    landed = true;
                    foreach (var w in ws) Unwarn(w);
                    foreach (var (c, r) in circles)
                        if (InReach(c, r)) { HitPlayer(slamDamage, true, c); break; }
                    foreach (var (c, r) in circles) Splash(c, r);
                }
                yield return null;
            }
            Cooldown("slam", 9f);
            busy = false;
        }

        IEnumerator Charge()
        {
            busy = true;
            Loop("Idle", 1f);
            Play("Roar");                                                             // a short tell before the rush
            yield return Wait(0.45f);
            Face(player.transform.position);
            Loop("Charge", 1f);
            float travelled = 0f;
            float max = Mathf.Min(16f, Flat(player.transform.position - transform.position).magnitude + 3f);
            while (travelled < max)
            {
                float step = chargeSpeed * Speed * Time.deltaTime;
                if (!Step(transform.forward * step)) break;                             // reached the island's edge
                travelled += step;
                if (InReach(transform.position + transform.forward * (biteReach * 0.7f), 2.4f))
                {
                    HitPlayer(chargeDamage, true, transform.position + transform.forward * biteReach);
                    break;
                }
                yield return null;
            }
            Loop("Idle", 1f);
            yield return Wait(0.5f);
            Cooldown("charge", 7f);
            busy = false;
        }

        // ---------------------------------------------------------------- hits taken

        void OnDamaged(DamageInfo info)
        {
            if (!phase2 && !phase2Pending && health.Current <= health.Max * 0.5f && !health.IsDead) phase2Pending = true;
        }

        public void OnHit(DamageInfo info, Vector3 impulse)
        {
            flash = 1f;
            FloatingText.Spawn(info.HitPoint != Vector3.zero ? info.HitPoint + Vector3.up * 0.8f : transform.position + Vector3.up * 3f,
                Mathf.RoundToInt(info.Amount).ToString(), info.IsHeavy ? new Color(1f, 0.55f, 0.2f) : Color.white);
            // a heavy blow or enough small ones make it flinch, but only between attacks (it does not stop mid-attack)
            poise += info.IsHeavy ? 40f : info.Amount;
            if (poise >= 40f && !busy && !dead && awake && !phase2Pending)
            {
                poise = 0f;
                StartCoroutine(Flinch());
            }
        }

        public void ApplySlow(float factor, float seconds)
        {
            slowFactor = Mathf.Clamp(factor, 0.2f, 1f);
            slowUntil = Time.time + seconds;
        }

        IEnumerator Flinch()
        {
            busy = true;
            Play("Hit");
            yield return Wait(0.5f);
            busy = false;
        }

        void OnDied(DamageInfo _)
        {
            if (dead) return;
            dead = true;
            StopAllAttacks();
            StartCoroutine(Die());
        }

        IEnumerator Die()
        {
            Play("Death");
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            yield return new WaitForSeconds(1.5f);
            if (bar != null) bar.Show(false);
            yield return new WaitForSeconds(1f);
            if (reward != null) GiftTracker.Collect(reward);
        }

        // ---------------------------------------------------------------- helpers

        bool Ready(string key) => !readyAt.TryGetValue(key, out float at) || Time.time >= at;

        void Cooldown(string key, float seconds) => readyAt[key] = Time.time + seconds / (phase2 ? phase2Speedup : 1f);

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        Vector3 Ground(Vector3 p) => new(p.x, groundY, p.z);

        WaitForSeconds Wait(float seconds) => new(seconds / Speed);

        void Turn(Vector3 direction, float degreesPerSecond)
        {
            if (direction.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), degreesPerSecond * Speed * Time.deltaTime);
        }

        void Face(Vector3 point)
        {
            Vector3 d = Flat(point - transform.position);
            if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(d);
        }

        // moves on the island, never past its edge; false when the edge stopped it
        bool Step(Vector3 delta)
        {
            Vector3 next = transform.position + delta;
            Vector3 off = Flat(next - arenaCenter.position);
            bool inside = off.magnitude <= arenaRadius;
            if (!inside) next = arenaCenter.position + off.normalized * arenaRadius;
            next.y = groundY;
            transform.position = next;
            return inside;
        }

        IEnumerator WalkTo(Vector3 target, float speed)
        {
            Loop("Walk", 1.2f);
            Vector3 from = transform.position;
            Face(target);
            float time = Mathf.Max(0.1f, Vector3.Distance(Flat(from), Flat(target)) / speed);
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                transform.position = Vector3.Lerp(from, target, t / time);
                yield return null;
            }
            transform.position = target;
        }

        bool InReach(Vector3 centre, float radius)
        {
            if (player == null || player.IsInvulnerable) return false;
            Vector3 p = player.transform.position;
            return Flat(p - centre).magnitude <= radius && Mathf.Abs(p.y - groundY) < 3f;
        }

        void HitPlayer(float amount, bool heavy, Vector3 from)
        {
            if (player == null) return;
            var info = new DamageInfo(amount, gameObject, from, heavy);
            if (!player.Health.TakeDamage(info)) return;
            Vector3 away = Flat(player.transform.position - from);
            away = away.sqrMagnitude > 0.01f ? away.normalized : transform.forward;
            player.GetComponentInParent<IHitReceiver>()?.OnHit(info, away * (heavy ? 5f : 3f) + Vector3.up * (heavy ? 2.5f : 1f));
            player.Shake(heavy ? 0.8f : 0.4f);
        }

        void Play(string state)
        {
            loopState = null;
            animator.speed = Speed;
            animator.CrossFadeInFixedTime(state, CrossFade);
        }

        void Loop(string state, float speed)
        {
            animator.speed = speed;
            if (loopState == state) return;
            loopState = state;
            animator.CrossFadeInFixedTime(state, 0.25f);
        }

        void UpdateFlash()
        {
            if (renderers == null) return;
            flash = Mathf.MoveTowards(flash, 0f, 5f * Time.deltaTime);
            Color tint = Color.Lerp(phase2 ? new Color(1f, 0.82f, 0.82f) : Color.white, new Color(1f, 0.35f, 0.3f), flash);
            block.SetColor("_BaseColor", tint);
            foreach (Renderer r in renderers) if (r != null) r.SetPropertyBlock(block);
        }

        // ---------------------------------------------------------------- warning circles, splashes, spit

        // A red circle on the ground: a pale rim at full size and a fill that grows to it over `lead` seconds (when the blow lands).
        GameObject Warn(Vector3 centre, float radius, float lead)
        {
            var root = new GameObject("Warning");
            root.transform.position = Ground(centre) + Vector3.up * 0.06f;
            Quad(root.transform, radius, new Color(1f, 0.12f, 0.08f, 0.35f));
            var fill = Quad(root.transform, radius, new Color(1f, 0.15f, 0.1f, 0.55f));
            StartCoroutine(GrowFill(fill.transform, lead));
            warnings.Add(root);
            return root;
        }

        GameObject Quad(Transform parent, float radius, Color color)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(parent, false);
            q.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            q.transform.localScale = Vector3.one * radius * 2f;
            var r = q.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (warningMaterial != null)
            {
                r.material = warningMaterial;
                r.material.SetColor("_TintColor", color);
                r.material.SetColor("_BaseColor", color);
            }
            return q;
        }

        static IEnumerator GrowFill(Transform fill, float lead)
        {
            Vector3 full = fill.localScale;
            for (float t = 0f; t < lead && fill != null; t += Time.deltaTime)
            {
                fill.localScale = full * Mathf.Max(0.02f, t / lead);
                yield return null;
            }
            if (fill != null) fill.localScale = full;
        }

        void Unwarn(GameObject w)
        {
            if (w == null) return;
            warnings.Remove(w);
            Destroy(w);
        }

        void ClearWarnings()
        {
            foreach (var w in warnings) if (w != null) Destroy(w);
            warnings.Clear();
        }

        // a white ring that spreads and fades where a tail hit the ground
        void Splash(Vector3 centre, float radius)
        {
            var root = new GameObject("Splash");
            root.transform.position = Ground(centre) + Vector3.up * 0.08f;
            var q = Quad(root.transform, radius, new Color(0.85f, 0.95f, 1f, 0.6f));
            StartCoroutine(FadeSplash(root, q.GetComponent<MeshRenderer>()));
        }

        static IEnumerator FadeSplash(GameObject root, MeshRenderer r)
        {
            Vector3 s = root.transform.localScale;
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                if (root == null) yield break;
                root.transform.localScale = s * (1f + t * 0.8f);
                Color c = new(0.85f, 0.95f, 1f, 0.6f * (1f - t / 0.5f));
                r.material.SetColor("_TintColor", c);
                r.material.SetColor("_BaseColor", c);
                yield return null;
            }
            Destroy(root);
        }

        void SpawnSpit(Vector3 from, Vector3 direction)
        {
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "WaterSpit";
            Destroy(ball.GetComponent<Collider>());
            ball.transform.position = from;
            ball.transform.localScale = Vector3.one * 0.9f;
            var r = ball.GetComponent<MeshRenderer>();
            if (spitMaterial != null) r.sharedMaterial = spitMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            StartCoroutine(FlySpit(ball.transform, direction.normalized));
        }

        IEnumerator FlySpit(Transform ball, Vector3 direction)
        {
            for (float t = 0f; t < 3f; t += Time.deltaTime)
            {
                if (ball == null) yield break;
                ball.position += direction * (spitSpeed * Time.deltaTime);
                ball.localScale = Vector3.one * (0.9f + 0.08f * Mathf.Sin(t * 30f));
                if (player != null && !player.IsInvulnerable && Vector3.Distance(ball.position, player.transform.position + Vector3.up * 1f) < 1.1f)
                {
                    HitPlayer(spitDamage, false, ball.position - direction * 2f);
                    player.GetComponentInParent<IHitReceiver>()?.ApplySlow(0.6f, 1.5f);   // soaked: slower for a moment
                    Splash(ball.position, 1.2f);
                    break;
                }
                if (ball.position.y < groundY - 0.2f) { Splash(ball.position, 1.4f); break; }
                yield return null;
            }
            if (ball != null) Destroy(ball.gameObject);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position + transform.forward * biteReach, biteRadius);
            Gizmos.color = new Color(1f, 0.4f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, sweepRadius);
            if (arenaCenter == null) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(arenaCenter.position, arenaRadius);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(arenaCenter.position, wakeRadius);
        }
    }
}
