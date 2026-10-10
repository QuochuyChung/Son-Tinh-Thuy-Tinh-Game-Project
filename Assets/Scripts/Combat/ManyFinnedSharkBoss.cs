using SonTinhThuyTinh.Player;
using UnityEngine;

namespace SonTinhThuyTinh.Combat
{
    // Arena AI for Map 9 Vay. The root's +Z is the shark's head direction, so every
    // frame rotates that axis toward Thuy Tinh before swimming or resolving a bite.
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(CapsuleCollider))]
    [DisallowMultipleComponent]
    public sealed class ManyFinnedSharkBoss : MonoBehaviour, IHitReceiver
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [Header("Movement")]
        [SerializeField, Min(0f)] float moveSpeed = 3.8f;
        [SerializeField, Min(1f)] float turnSpeed = 240f;
        [SerializeField, Min(0f)] float stopDistance = 4.5f;

        [Header("Attack")]
        [SerializeField, Min(0f)] float attackRange = 5.8f;
        [SerializeField, Min(0f)] float attackDamage = 14f;
        [SerializeField, Min(0f)] float attackWindup = 0.48f;
        [SerializeField, Min(0.01f)] float attackDuration = 1.05f;
        [SerializeField, Min(0f)] float attackCooldown = 0.9f;

        Animator animator;
        Health health;
        PlayerController player;
        Renderer[] renderers;
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
                animator.enabled = true;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Rebind();
                animator.Update(0f);
            }

            renderers = GetComponentsInChildren<Renderer>(true);
            propertyBlock = new MaterialPropertyBlock();
            baseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
                baseColors[i] = renderers[i].sharedMaterial != null && renderers[i].sharedMaterial.HasProperty(BaseColor)
                    ? renderers[i].sharedMaterial.GetColor(BaseColor) : Color.white;
            health.Died += OnDied;
        }

        void Start()
        {
            player = FindFirstObjectByType<PlayerController>();
            Play("Shark_Swim", 0f);
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
                TickAttack(distance, facingTarget);
                return;
            }

            if (distance <= attackRange && facingTarget >= 0.92f && Time.time >= nextAttackAt)
            {
                attacking = true;
                attackLanded = false;
                attackElapsed = 0f;
                Play("Shark_Attack", 0.06f, true);
                return;
            }

            if (distance > stopDistance)
            {
                float factor = Time.time < slowUntil ? slowFactor : 1f;
                float travel = Mathf.Min(moveSpeed * factor * Time.deltaTime, Mathf.Max(0f, distance - stopDistance));
                transform.position += toPlayer.normalized * travel;
            }
            Play("Shark_Swim", 0.12f);
        }

        void TickAttack(float distance, float facingTarget)
        {
            attackElapsed += Time.deltaTime;
            if (!attackLanded && attackElapsed >= attackWindup)
            {
                attackLanded = true;
                if (distance <= attackRange + 0.65f && facingTarget >= 0.65f)
                {
                    Vector3 point = player.transform.position + Vector3.up;
                    if (player.Health.TakeDamage(new DamageInfo(attackDamage, gameObject, point, true)))
                        FloatingText.Spawn(point + Vector3.up, Mathf.RoundToInt(attackDamage).ToString(), new Color(0.2f, 0.78f, 1f));
                }
            }

            if (attackElapsed < attackDuration) return;
            attacking = false;
            nextAttackAt = Time.time + attackCooldown;
            Play("Shark_Swim", 0.1f);
        }

        void Play(string state, float fade, bool restart = false)
        {
            if (animator == null) return;
            int stateHash = Animator.StringToHash(state);
            if (!restart && requestedState == stateHash) return;
            requestedState = stateHash;
            animator.CrossFadeInFixedTime(stateHash, fade, 0, 0f);
        }

        void OnDied(DamageInfo _)
        {
            battleEnded = true;
            attacking = false;
            Play("Shark_Defeated", 0.08f, true);
        }

        public void SetBattleEnded()
        {
            battleEnded = true;
            if (!health.IsDead) Play("Shark_Swim", 0.1f);
        }

        public void OnHit(DamageInfo info, Vector3 impulse)
        {
            flash = 1f;
            Vector3 point = info.HitPoint == default ? transform.position + Vector3.up * 1.5f : info.HitPoint;
            FloatingText.Spawn(point, Mathf.RoundToInt(info.Amount).ToString(),
                info.IsHeavy ? new Color(1f, 0.7f, 0.2f) : Color.white);
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
