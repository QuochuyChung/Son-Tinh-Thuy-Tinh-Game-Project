using SonTinhThuyTinh.Player;
using UnityEngine;

namespace SonTinhThuyTinh.Combat
{
    // Arena AI for Rua Chin Chan. The imported model faces local +Z in Unity.
    // The root is turned toward Thuy Tinh every frame, including during attacks,
    // so the turtle's head never attacks away from the player.
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(CapsuleCollider))]
    [DisallowMultipleComponent]
    public sealed class NineLeggedTurtleBoss : MonoBehaviour, IHitReceiver
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int ColorProperty = Shader.PropertyToID("_Color");

        [Header("Movement")]
        [SerializeField, Min(0f)] float moveSpeed = 2.8f;
        [SerializeField, Min(1f)] float turnSpeed = 260f;
        [SerializeField, Min(0f)] float stopDistance = 3.15f;

        [Header("Attack")]
        [SerializeField, Min(0f)] float attackRange = 3.9f;
        [SerializeField, Min(0f)] float attackDamage = 18f;
        [SerializeField, Min(0f)] float attackWindup = 0.78f;
        [SerializeField, Min(0.01f)] float attackDuration = 1.5f;
        [SerializeField, Min(0f)] float attackCooldown = 0.85f;

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
            {
                Material material = renderers[i].sharedMaterial;
                baseColors[i] = material != null && material.HasProperty(BaseColor)
                    ? material.GetColor(BaseColor)
                    : material != null && material.HasProperty(ColorProperty)
                        ? material.GetColor(ColorProperty)
                        : Color.white;
            }
            health.Died += OnDied;
        }

        void Start()
        {
            player = FindFirstObjectByType<PlayerController>();
            Play("Turtle_Move", 0f);
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

            // Face Thuy Tinh first on every frame. This continues while the attack
            // animation is playing, keeping the dragon head locked on its target.
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

            if (distance <= attackRange && facingTarget >= 0.9f && Time.time >= nextAttackAt)
            {
                attacking = true;
                attackLanded = false;
                attackElapsed = 0f;
                Play("Turtle_Attack", 0.06f, true);
                return;
            }

            if (distance > stopDistance)
            {
                float factor = Time.time < slowUntil ? slowFactor : 1f;
                float travel = Mathf.Min(moveSpeed * factor * Time.deltaTime, Mathf.Max(0f, distance - stopDistance));
                transform.position += toPlayer.normalized * travel;
            }
            Play("Turtle_Move", 0.12f);
        }

        void TickAttack(float distance, float facingTarget)
        {
            attackElapsed += Time.deltaTime;
            if (!attackLanded && attackElapsed >= attackWindup)
            {
                attackLanded = true;
                if (distance <= attackRange + 0.75f && facingTarget >= 0.6f)
                {
                    Vector3 point = player.transform.position + Vector3.up;
                    if (player.Health.TakeDamage(new DamageInfo(attackDamage, gameObject, point, true)))
                        FloatingText.Spawn(point + Vector3.up, Mathf.RoundToInt(attackDamage).ToString(), new Color(0.35f, 1f, 0.55f));
                }
            }

            if (attackElapsed < attackDuration) return;
            attacking = false;
            nextAttackAt = Time.time + attackCooldown;
            Play("Turtle_Move", 0.1f);
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
            Play("Turtle_Defeated", 0.08f, true);
        }

        public void SetBattleEnded()
        {
            battleEnded = true;
            if (!health.IsDead) Play("Turtle_Move", 0.1f);
        }

        public void OnHit(DamageInfo info, Vector3 impulse)
        {
            flash = 1f;
            Vector3 point = info.HitPoint == default ? transform.position + Vector3.up : info.HitPoint;
            FloatingText.Spawn(point, Mathf.RoundToInt(info.Amount).ToString(),
                info.IsHeavy ? new Color(1f, 0.72f, 0.2f) : Color.white);
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
                renderers[i].GetPropertyBlock(propertyBlock);
                Color color = Color.Lerp(baseColors[i], new Color(1f, 0.25f, 0.18f), flash);
                propertyBlock.SetColor(BaseColor, color);
                propertyBlock.SetColor(ColorProperty, color);
                renderers[i].SetPropertyBlock(propertyBlock);
            }
        }
    }
}
