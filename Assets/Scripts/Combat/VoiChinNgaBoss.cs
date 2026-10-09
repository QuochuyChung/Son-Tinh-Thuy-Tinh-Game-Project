using SonTinhThuyTinh.Player;
using UnityEngine;

namespace SonTinhThuyTinh.Combat
{
    // Small arena AI for the nine-tusk elephant. It chases, plays the authored Attack
    // clip and applies one hit during the impact window. Player attacks use the normal
    // Health/IHitReceiver path, so every existing Son Tinh move and spell works on it.
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(CapsuleCollider))]
    [DisallowMultipleComponent]
    public class VoiChinNgaBoss : MonoBehaviour, IHitReceiver
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [Header("Movement")]
        [SerializeField, Min(0f)] float moveSpeed = 2.8f;
        [SerializeField, Min(1f)] float turnSpeed = 130f;
        [SerializeField, Min(0f)] float stopDistance = 4.7f;

        [Header("Attack")]
        [SerializeField, Min(0f)] float attackRange = 6.2f;
        [SerializeField, Min(0f)] float attackDamage = 18f;
        [SerializeField, Min(0f)] float attackWindup = 0.58f;
        [SerializeField, Min(0.01f)] float attackDuration = 1.15f;
        [SerializeField, Min(0f)] float attackCooldown = 1.1f;

        Animator animator;
        Health health;
        PlayerController player;
        Renderer[] renderers;
        Transform visualRoot;
        MaterialPropertyBlock propertyBlock;
        Color[] baseColors;
        float attackElapsed;
        float nextAttackAt;
        float flash;
        float slowFactor = 1f;
        float slowUntil;
        int requestedState;
        bool attacking;
        bool attackLanded;
        bool battleEnded;

        public Health Health => health;

        void Awake()
        {
            health = GetComponent<Health>();
            animator = GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                // The large skinned bounds can briefly fall outside the camera while the
                // boss turns. Keep evaluating the rig so Walk/Attack never freezes.
                animator.enabled = true;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.speed = 1f;
                animator.Rebind();
                animator.Update(0f);
                visualRoot = animator.transform;
            }
            renderers = GetComponentsInChildren<Renderer>(true);
            propertyBlock = new MaterialPropertyBlock();
            baseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
                baseColors[i] = renderers[i].sharedMaterial != null && renderers[i].sharedMaterial.HasProperty(BaseColor)
                    ? renderers[i].sharedMaterial.GetColor(BaseColor) : Color.white;
            SnapVisualToGround();
            health.Died += OnDied;
        }

        void Start()
        {
            player = FindFirstObjectByType<PlayerController>();
            Play("Idle", 0f);
        }

        void OnDestroy()
        {
            if (health != null) health.Died -= OnDied;
        }

        void Update()
        {
            UpdateFlash();
            if (battleEnded || health.IsDead) return;
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            if (player == null || player.Health.IsDead) return;

            Vector3 toPlayer = player.transform.position - transform.position;
            toPlayer.y = 0f;
            float distance = toPlayer.magnitude;
            float facingTarget = 1f;
            if (toPlayer.sqrMagnitude > 0.01f)
            {
                Quaternion facing = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, turnSpeed * Time.deltaTime);
                facingTarget = Vector3.Dot(transform.forward, toPlayer.normalized);
            }

            if (attacking)
            {
                TickAttack(distance);
                return;
            }

            // Finish turning the head/body toward Son Tinh before committing to a hit.
            // This prevents the elephant from beginning an attack while its rear faces him.
            if (distance <= attackRange && facingTarget >= 0.8f && Time.time >= nextAttackAt)
            {
                attacking = true;
                attackLanded = false;
                attackElapsed = 0f;
                Play("Attack", 0.08f, true);
                return;
            }

            if (distance > stopDistance)
            {
                float factor = Time.time < slowUntil ? slowFactor : 1f;
                Vector3 step = toPlayer.normalized * (moveSpeed * factor * Time.deltaTime);
                if (step.magnitude > distance - stopDistance) step = step.normalized * Mathf.Max(0f, distance - stopDistance);
                transform.position += step;
                Play("Walk_Forward", 0.12f);
            }
            else
            {
                Play("Idle", 0.12f);
            }
        }

        void TickAttack(float distance)
        {
            attackElapsed += Time.deltaTime;
            if (!attackLanded && attackElapsed >= attackWindup)
            {
                attackLanded = true;
                float facing = Vector3.Dot(transform.forward, (player.transform.position - transform.position).normalized);
                if (distance <= attackRange + 0.8f && facing > 0.15f)
                {
                    Vector3 point = player.transform.position + Vector3.up;
                    player.Health.TakeDamage(new DamageInfo(attackDamage, gameObject, point, true));
                    FloatingText.Spawn(point + Vector3.up, Mathf.RoundToInt(attackDamage).ToString(), new Color(1f, 0.35f, 0.2f));
                }
            }

            if (attackElapsed < attackDuration) return;
            attacking = false;
            nextAttackAt = Time.time + attackCooldown;
            Play("Idle", 0.1f);
        }

        void Play(string state, float fade, bool restart = false)
        {
            if (animator == null) return;
            int stateHash = Animator.StringToHash(state);
            // During a cross-fade GetCurrentAnimatorStateInfo still reports the previous
            // state. Tracking the requested state prevents restarting the same transition
            // every frame, which previously left Walk_Forward permanently at frame zero.
            if (!restart && requestedState == stateHash) return;
            requestedState = stateHash;
            animator.CrossFadeInFixedTime(stateHash, fade, 0, 0f);
        }

        // The skinned renderer bounds include the long tusks and carriage, so they are not
        // a reliable ground reference. Each foot bone starts at the ankle and ends 0.04
        // model units lower at the sole; use those authored rig measurements instead.
        void SnapVisualToGround()
        {
            if (visualRoot == null) return;
            Transform[] bones = visualRoot.GetComponentsInChildren<Transform>(true);
            float lowestAnkle = float.PositiveInfinity;
            int feet = 0;
            foreach (Transform bone in bones)
                if (bone.name.EndsWith(".foot", System.StringComparison.OrdinalIgnoreCase))
                {
                    lowestAnkle = Mathf.Min(lowestAnkle, bone.position.y);
                    feet++;
                }

            if (feet > 0)
            {
                float soleBelowAnkle = 0.04f * Mathf.Abs(visualRoot.lossyScale.x);
                float desiredAnkle = transform.position.y + soleBelowAnkle;
                visualRoot.position += Vector3.up * (desiredAnkle - lowestAnkle + 0.01f);
                return;
            }

            // Safe fallback if a future FBX renames its bones.
            if (renderers == null || renderers.Length == 0) return;
            float lowest = renderers[0].bounds.min.y;
            for (int i = 1; i < renderers.Length; i++) lowest = Mathf.Min(lowest, renderers[i].bounds.min.y);
            visualRoot.position += Vector3.up * (transform.position.y - lowest + 0.01f);
        }

        void OnDied(DamageInfo _)
        {
            battleEnded = true;
            attacking = false;
            Play("Death", 0.08f);
        }

        public void SetBattleEnded()
        {
            battleEnded = true;
            if (!health.IsDead) Play("Idle", 0.1f);
        }

        public void OnHit(DamageInfo info, Vector3 impulse)
        {
            flash = 1f;
            FloatingText.Spawn(info.HitPoint == default ? transform.position + Vector3.up * 5f : info.HitPoint,
                Mathf.RoundToInt(info.Amount).ToString(), info.IsHeavy ? new Color(1f, 0.65f, 0.2f) : Color.white);
        }

        public void ApplySlow(float factor, float seconds)
        {
            slowFactor = Mathf.Clamp(factor, 0.1f, 1f);
            slowUntil = Mathf.Max(slowUntil, Time.time + seconds);
        }

        void UpdateFlash()
        {
            flash = Mathf.MoveTowards(flash, 0f, 4f * Time.deltaTime);
            for (int i = 0; i < renderers.Length; i++)
            {
                propertyBlock.SetColor(BaseColor, Color.Lerp(baseColors[i], Color.white, flash));
                renderers[i].SetPropertyBlock(propertyBlock);
            }
        }
    }
}
