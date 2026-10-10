using System;
using System.Collections;
using SonTinhThuyTinh.Player;
using SonTinhThuyTinh.UI;
using UnityEngine;

namespace SonTinhThuyTinh.Combat
{
    // One of the rooster thieves on Map_SonTinh (docs/ke-hoach-ga-chin-cua.md), an NPC run by the computer. The Animator only "acts" (states
    // cross-faded by name, clips from Mixamo, all in place); this script moves it, picks its moves and checks its hits by distance at the frame
    // each clip lands (measured on the clips: tools, docs/progress.md 9.20).
    //  - far (> runRange): Run after the player;  near: Strafe round the player (left or right), facing them, waiting for its turn;
    //  - its turn: runs straight in to striking distance, then strikes (it does not only circle);
    //  - hugged for a while (< kickRange for kickAfter seconds): now and then a Kick that shoves the player back a little (close combat is
    //    the point of the fight, so it is rare and short);  its turn (RoosterQuestDirector hands it out, one Ninja at a time):
    //    Attack (jump attack, normal), Combo (three cuts, a stronger normal attack, wound up longer) or Slash (its strong blow);
    //  - hit: Impact, the move it was making is lost (the player's chance to strike back);
    //  - guard: blocks the player's NORMAL attacks now and then (Block clip, no damage); spells (U / I / O) always get through;
    //  - Death at zero health.
    // The sword hangs on its back until DrawSword (Draw clip), then sits in the right hand.
    [RequireComponent(typeof(Health))]
    public class NinjaEnemy : MonoBehaviour, IHitReceiver
    {
        [Header("Parts")]
        [SerializeField] Animator animator;
        [SerializeField] Transform sword;
        [SerializeField] Transform handSocket;
        [SerializeField] Transform backSocket;
        [SerializeField] WorldHealthBar healthBar;
        [SerializeField] Renderer[] renderers;

        [Header("Movement")]
        [SerializeField] float runSpeed = 4.2f;
        [SerializeField] float strafeSpeed = 2.4f;
        [SerializeField] float turnSpeed = 360f;
        [SerializeField] float runRange = 5.5f;
        [SerializeField] float strafeRadius = 3.2f;

        [Header("Moves")]
        [SerializeField] float kickRange = 1.1f;
        [Tooltip("Seconds the player has to stay that close before it kicks.")]
        [SerializeField] float kickAfter = 1.5f;
        [SerializeField] float kickCooldown = 9f;
        [SerializeField] float kickPush = 3f;
        [SerializeField] float kickDamage = 5f;
        [SerializeField] float attackDamage = 8f;     // jump attack
        [SerializeField] float comboDamage = 6f;      // each of the three cuts
        [SerializeField] float slashDamage = 18f;
        [Tooltip("Reach of a sword blow, from the pivot along the facing.")]
        [SerializeField] float reach = 2.2f;
        [SerializeField] float hitRadius = 1.5f;

        [Header("Guard")]
        [Range(0f, 1f)] [SerializeField] float blockChance = 0.25f;
        [Tooltip("Each recent hit taken adds this to the block chance (spamming the same attack gets blocked more), up to maxBlockChance.")]
        [SerializeField] float blockPerRecentHit = 0.08f;
        [Range(0f, 1f)] [SerializeField] float maxBlockChance = 0.5f;

        const float Fade = 0.15f;

        Health health;
        PlayerController player;
        Transform arenaCenter;
        float arenaRadius = 10f;
        Func<NinjaEnemy, bool> requestTurn;
        Action<NinjaEnemy> endTurn;
        Coroutine move;
        bool fighting, busy, dead, hasTurn;
        int strafeSide = 1;
        float nextStrafeFlip, nextAttackAt, nextKickAt, closeSince = -1f, flash, recentHits, lastHitAt = -10f;
        string loopState;
        MaterialPropertyBlock block;

        public event Action<NinjaEnemy> Killed;
        public bool IsDead => dead;
        public bool IsArmed => sword != null && sword.parent == handSocket;
        public Health Health => health;

        void Awake()
        {
            health = GetComponent<Health>();
            block = new MaterialPropertyBlock();
            health.Guard = TryBlock;
            health.Died += _ => Die();
        }

        public void Setup(Transform centre, float radius, Func<NinjaEnemy, bool> request, Action<NinjaEnemy> release)
        {
            arenaCenter = centre; arenaRadius = radius; requestTurn = request; endTurn = release;
        }

        // ---------------------------------------------------------------- cutscene / director controls

        public void Act(string state, float fade = Fade) { loopState = null; animator.speed = 1f; animator.CrossFadeInFixedTime(state, fade); }

        public void Face(Vector3 point)
        {
            Vector3 d = point - transform.position; d.y = 0f;
            if (d.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(d);
        }

        // The arms-only layer (AC_Ninja "HoldArms", RoosterQuestBuilder): the leader keeps hugging the rooster while his legs walk or run.
        public void SetHolding(bool on)
        {
            if (animator.layerCount > 1) animator.SetLayerWeight(1, on ? 1f : 0f);
        }

        public IEnumerator RunTo(Vector3 target, float speed, string state = "Run")
        {
            Loop(state, state == "Run" ? Mathf.Clamp(speed / runSpeed, 0.45f, 1.5f) : 1f);   // the legs keep pace with the ground
            Vector3 d = target - transform.position; d.y = 0f;
            while (d.magnitude > 0.2f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), turnSpeed * Time.deltaTime);
                transform.position = Grounded(transform.position + d.normalized * Mathf.Min(d.magnitude, speed * Time.deltaTime));
                yield return null;
                d = target - transform.position; d.y = 0f;
            }
            Loop("Idle");
        }

        public IEnumerator DrawSword()
        {
            Act("Draw", 0.1f);
            yield return new WaitForSeconds(0.25f);
            Hold(sword, handSocket);
            yield return new WaitForSeconds(0.35f);
            Loop("Idle");   // sword in hand, breathing on the spot until the fight starts (Strafe here slid its feet in place)
        }

        public void BeginFight()
        {
            FindPlayer();
            fighting = true; busy = false;
            nextAttackAt = Time.time + UnityEngine.Random.Range(0.6f, 1.6f);
            if (healthBar != null) healthBar.Show(true);
        }

        public void StopFight()
        {
            fighting = false;
            if (move != null) StopCoroutine(move);
            move = null; busy = false;
            ReleaseTurn();
            if (!dead) Loop("Idle");
        }

        // back to the start of the fight (the player lost): full health, sword on the back, standing at `pos`
        public void ResetTo(Vector3 pos, Quaternion rot)
        {
            StopAllCoroutines();
            move = null; fighting = false; busy = false; dead = false; hasTurn = false; recentHits = 0f;
            gameObject.SetActive(true);
            foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = true;
            health.Revive();
            transform.SetPositionAndRotation(Grounded(pos), rot);
            Hold(sword, backSocket);
            if (healthBar != null) healthBar.Show(false);
            animator.Rebind();
            SetHolding(false);
            Loop("Idle");
        }

        static void Hold(Transform item, Transform socket)
        {
            if (item == null || socket == null) return;
            item.SetParent(socket, false);
            item.localPosition = Vector3.zero; item.localRotation = Quaternion.identity;
        }

        // ---------------------------------------------------------------- fight loop

        void Update()
        {
            UpdateFlash();
            if (!fighting || dead || busy) return;
            FindPlayer();
            if (player == null || player.Health.IsDead) { Loop("Idle"); return; }

            Vector3 to = Flat(player.transform.position - transform.position);
            float dist = to.magnitude;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to.sqrMagnitude > 0.01f ? to : transform.forward), turnSpeed * Time.deltaTime);

            if (dist < kickRange) { if (closeSince < 0f) closeSince = Time.time; } else closeSince = -1f;
            if (closeSince >= 0f && Time.time - closeSince >= kickAfter && Time.time >= nextKickAt) { closeSince = -1f; move = StartCoroutine(Kick()); return; }
            if (dist > runRange)
            {
                ReleaseTurn();
                Loop("Run");
                Step(to.normalized * (runSpeed * Time.deltaTime));
                return;
            }
            if (Time.time >= nextAttackAt && TakeTurn())
            {
                if (dist > reach + 0.4f)
                {
                    // its turn: straight at the player (a slower run), then the blow
                    Loop("Run", 0.85f);
                    Step(to.normalized * (runSpeed * 0.85f * Time.deltaTime));
                    return;
                }
                float r = UnityEngine.Random.value;
                move = StartCoroutine(r < 0.4f ? JumpAttack() : r < 0.7f ? Slash() : Combo());
                return;
            }
            // wait for a turn: circle round the player at strafeRadius, changing side now and then
            if (Time.time >= nextStrafeFlip) { strafeSide = -strafeSide; nextStrafeFlip = Time.time + UnityEngine.Random.Range(1.5f, 3.5f); }
            Vector3 side = Vector3.Cross(Vector3.up, to.normalized) * strafeSide;
            Vector3 radial = to.normalized * Mathf.Clamp(dist - strafeRadius, -1f, 1f);
            Loop(strafeSide > 0 ? "Strafe" : "StrafeR");
            Step((side + radial * 0.8f).normalized * (strafeSpeed * Time.deltaTime));
        }

        bool TakeTurn()
        {
            if (hasTurn) return true;
            hasTurn = requestTurn == null || requestTurn(this);
            return hasTurn;
        }

        void ReleaseTurn()
        {
            if (!hasTurn) return;
            hasTurn = false;
            endTurn?.Invoke(this);
        }

        IEnumerator JumpAttack()
        {
            busy = true;
            Act("Attack");
            yield return new WaitForSeconds(0.25f);
            yield return Lunge(0.25f, 1.15f, 3.2f);                // the leap forward (the clip's own travel, taken out of the clip)
            yield return HitAt(1.20f - 1.15f, attackDamage, false);
            yield return new WaitForSeconds(2.43f - 1.20f - 0.3f);
            EndMove(1.6f);
        }

        IEnumerator Combo()
        {
            busy = true;
            Act("Combo");
            float t = 0f;
            foreach (float at in new[] { 0.63f, 1.33f, 2.17f })
            {
                yield return Lunge(t, at, 0.55f);
                yield return HitAt(0f, comboDamage, false);
                t = at;
            }
            yield return new WaitForSeconds(3.27f - 2.17f - 0.3f);
            EndMove(1.8f);
        }

        IEnumerator Slash()
        {
            busy = true;
            Act("Slash");
            yield return Lunge(0f, 0.83f, 0.6f, true);
            yield return HitAt(0f, slashDamage, true);
            yield return new WaitForSeconds(1.67f - 0.83f - 0.1f);   // the recovery: the opening to strike back
            EndMove(2.2f);
        }

        IEnumerator Kick()
        {
            busy = true;
            Act("Kick");
            yield return new WaitForSeconds(0.40f);
            if (InReach(transform.position + transform.forward * 0.9f, 1.3f)) Hurt(kickDamage, false, kickPush);
            yield return new WaitForSeconds(1.20f - 0.40f - 0.2f);
            nextKickAt = Time.time + kickCooldown;
            EndMove(0.8f);
        }

        void EndMove(float cooldown)
        {
            busy = false; move = null;
            nextAttackAt = Time.time + cooldown + UnityEngine.Random.Range(0f, 0.8f);
            ReleaseTurn();
            loopState = null;
        }

        // moves forward `distance` metres between clip times `from` and `to`, turning towards the player before the blow when `track`
        IEnumerator Lunge(float from, float to, float distance, bool track = true)
        {
            float duration = Mathf.Max(0.01f, to - from);
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                if (track && player != null)
                {
                    Vector3 d = Flat(player.transform.position - transform.position);
                    if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), 200f * Time.deltaTime);
                    if (d.magnitude < 1.2f) { yield return null; continue; }   // do not run through the player
                }
                Step(transform.forward * (distance / duration * Time.deltaTime));
                yield return null;
            }
        }

        IEnumerator HitAt(float wait, float damage, bool heavy)
        {
            if (wait > 0f) yield return new WaitForSeconds(wait);
            if (InReach(transform.position + transform.forward * reach * 0.6f, hitRadius + reach * 0.4f)) Hurt(damage, heavy, heavy ? 6f : 3f);
        }

        bool InReach(Vector3 centre, float radius)
        {
            if (player == null || player.IsInvulnerable || player.Health.IsDead) return false;
            Vector3 p = player.transform.position;
            return Flat(p - centre).magnitude <= radius && Mathf.Abs(p.y - transform.position.y) < 2f;
        }

        void Hurt(float amount, bool heavy, float push)
        {
            var info = new DamageInfo(amount, gameObject, transform.position + transform.forward * 0.8f + Vector3.up, heavy);
            if (!player.Health.TakeDamage(info)) return;
            player.GetComponentInParent<IHitReceiver>()?.OnHit(info, Flat(player.transform.position - transform.position).normalized * push + Vector3.up * (heavy ? 2f : 0.5f));
            player.Shake(heavy ? 0.5f : 0.25f);
        }

        // ---------------------------------------------------------------- being hit

        bool TryBlock(DamageInfo info)
        {
            if (!fighting || dead || busy || !info.IsNormalAttack) return false;      // only between its own moves, never spells
            Vector3 from = info.Source != null ? info.Source.transform.position : info.HitPoint;
            if (Vector3.Angle(transform.forward, Flat(from - transform.position)) > 75f) return false;   // not from behind
            float chance = Mathf.Min(maxBlockChance, blockChance + blockPerRecentHit * recentHits);
            if (UnityEngine.Random.value > chance) return false;
            Act("Block", 0.05f);
            FloatingText.Spawn(transform.position + Vector3.up * 2.2f, "Đỡ!", new Color(0.75f, 0.85f, 1f));
            recentHits = 0f;
            nextAttackAt = Mathf.Min(nextAttackAt, Time.time + 0.5f);   // a block is followed by a quick riposte
            StartCoroutine(BackToLoop(0.6f));
            return true;
        }

        IEnumerator BackToLoop(float after)
        {
            busy = true;
            yield return new WaitForSeconds(after);
            busy = false; loopState = null;
        }

        public void OnHit(DamageInfo info, Vector3 impulse)
        {
            if (dead) return;
            flash = 1f;
            recentHits = (Time.time - lastHitAt < 2.5f ? recentHits : 0f) + 1f;
            lastHitAt = Time.time;
            FloatingText.Spawn(transform.position + Vector3.up * 2.2f, Mathf.RoundToInt(info.Amount).ToString(), info.IsHeavy ? new Color(1f, 0.55f, 0.2f) : Color.white);
            if (!fighting) return;
            // the hit breaks whatever move it was making: Impact, then it has to start again
            if (move != null) { StopCoroutine(move); move = null; }
            ReleaseTurn();
            Act("Impact", 0.05f);
            Step(Flat(impulse) * 0.06f);
            nextAttackAt = Time.time + 1.1f;
            StartCoroutine(BackToLoop(info.IsHeavy ? 0.8f : 0.55f));
        }

        public void ApplySlow(float factor, float seconds) { }

        void Die()
        {
            if (dead) return;
            dead = true; fighting = false; busy = true;
            StopAllCoroutines(); move = null;
            ReleaseTurn();
            Act("Death", 0.1f);
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            if (healthBar != null) healthBar.Show(false);
            Killed?.Invoke(this);
            StartCoroutine(Vanish());
        }

        IEnumerator Vanish()
        {
            yield return new WaitForSeconds(5f);
            gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------- helpers

        void FindPlayer()
        {
            if (player != null && player.isActiveAndEnabled) return;
            player = null;
            foreach (PlayerController c in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
                if (c.isActiveAndEnabled) { player = c; break; }
        }

        void Loop(string state, float speed = 1f)
        {
            animator.speed = speed;
            if (loopState == state) return;
            loopState = state;
            animator.CrossFadeInFixedTime(state, 0.2f);
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        void Step(Vector3 delta)
        {
            Vector3 next = transform.position + delta;
            if (arenaCenter != null)
            {
                Vector3 off = Flat(next - arenaCenter.position);
                if (off.magnitude > arenaRadius) next = arenaCenter.position + off.normalized * arenaRadius + Vector3.up * next.y;
            }
            // keep apart from the other Ninja
            foreach (var other in FindObjectsByType<NinjaEnemy>(FindObjectsSortMode.None))
            {
                if (other == this || other.dead) continue;
                Vector3 d = Flat(next - other.transform.position);
                if (d.magnitude < 1.2f && d.sqrMagnitude > 0.0001f) next += d.normalized * (1.2f - d.magnitude) * 0.5f;
            }
            transform.position = Grounded(next);
        }

        static Vector3 Grounded(Vector3 p)
        {
            Terrain t = Terrain.activeTerrain;
            if (t != null) p.y = t.SampleHeight(p) + t.transform.position.y;
            return p;
        }

        void UpdateFlash()
        {
            if (renderers == null || flash <= 0f) return;
            flash = Mathf.MoveTowards(flash, 0f, 5f * Time.deltaTime);
            block.SetColor("_BaseColor", Color.Lerp(Color.white, new Color(1f, 0.4f, 0.35f), flash));
            foreach (Renderer r in renderers) if (r != null) r.SetPropertyBlock(block);
        }
    }
}
