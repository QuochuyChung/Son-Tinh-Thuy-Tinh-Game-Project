using System.Collections;
using System.Collections.Generic;
using SonTinhThuyTinh.Player;
using UnityEngine;

namespace SonTinhThuyTinh.Combat
{
    // Arena AI for Rua Chin Chan. Its normal strike is joined by an alternating
    // shell spin and jade shockwave, both with readable ground telegraphs.
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(CapsuleCollider))]
    [DisallowMultipleComponent]
    public sealed class NineLeggedTurtleBoss : MonoBehaviour, IHitReceiver
    {
        enum AttackMode
        {
            None,
            Basic,
            ShellSpin,
            JadeShockwave
        }

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int ColorProperty = Shader.PropertyToID("_Color");

        [Header("Movement")]
        [SerializeField, Min(0f)] float moveSpeed = 2.8f;
        [SerializeField, Min(1f)] float turnSpeed = 260f;
        [SerializeField, Min(0f)] float stopDistance = 3.15f;

        [Header("Basic Attack")]
        [SerializeField, Min(0f)] float attackRange = 3.9f;
        [SerializeField, Min(0f)] float attackDamage = 18f;
        [SerializeField, Min(0f)] float attackWindup = 0.78f;
        [SerializeField, Min(0.01f)] float attackDuration = 1.5f;
        [SerializeField, Min(0f)] float attackCooldown = 0.85f;

        [Header("Special Attacks")]
        [SerializeField, Min(0f)] float specialOpeningDelay = 2.2f;
        [SerializeField, Min(0f)] float specialCooldown = 4.6f;
        [SerializeField, Min(1f)] float spinTriggerRange = 5.5f;
        [SerializeField, Min(0f)] float spinDamage = 24f;
        [SerializeField, Min(0.1f)] float spinWindup = 0.55f;
        [SerializeField, Min(0.1f)] float spinDuration = 1.65f;
        [SerializeField, Min(0f)] float spinMoveSpeed = 6.5f;
        [SerializeField, Min(0.1f)] float spinHitRadius = 3.1f;
        [SerializeField, Min(1f)] float shockwaveRadius = 9f;
        [SerializeField, Min(0f)] float shockwaveDamage = 27f;
        [SerializeField, Min(0.1f)] float shockwaveWindup = 1.05f;
        [SerializeField, Min(0.1f)] float shockwaveDuration = 1.55f;

        Animator animator;
        Health health;
        PlayerController player;
        Renderer[] renderers;
        MaterialPropertyBlock propertyBlock;
        Material telegraphMaterial;
        readonly List<GameObject> spawnedEffects = new List<GameObject>();
        Color[] baseColors;
        GameObject activeTelegraph;
        AttackMode attackMode;
        float attackElapsed;
        float nextAttackAt;
        float nextSpecialAt;
        float flash;
        float slowFactor = 1f;
        float slowUntil;
        int requestedState;
        bool attackLanded;
        bool battleEnded;
        bool useShockwaveNext = true;

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

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("HDRP/Unlit")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Unlit/Color");
            if (shader != null) telegraphMaterial = new Material(shader) { name = "Rua9Chan_Telegraph_Runtime" };
            health.Died += OnDied;
        }

        void Start()
        {
            player = FindFirstObjectByType<PlayerController>();
            nextSpecialAt = Time.time + specialOpeningDelay;
            Play("Turtle_Move", 0f);
        }

        void OnDestroy()
        {
            if (health != null) health.Died -= OnDied;
            CleanupEffects();
            if (telegraphMaterial != null) Destroy(telegraphMaterial);
        }

        void Update()
        {
            UpdateFlash();
            if (battleEnded || health.IsDead) return;
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            if (player == null || player.Health.IsDead) return;

            Vector3 toPlayer = Flat(player.transform.position - transform.position);
            float distance = toPlayer.magnitude;
            float facingTarget = 1f;
            if (attackMode != AttackMode.ShellSpin && toPlayer.sqrMagnitude > 0.01f)
            {
                Quaternion facing = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, turnSpeed * Time.deltaTime);
                facingTarget = Vector3.Dot(transform.forward, toPlayer.normalized);
            }

            if (attackMode != AttackMode.None)
            {
                TickAttack(distance, facingTarget, toPlayer);
                return;
            }

            if (Time.time >= nextSpecialAt && distance <= shockwaveRadius && facingTarget >= 0.75f)
            {
                if (!useShockwaveNext && distance <= spinTriggerRange)
                    BeginShellSpin();
                else
                    BeginJadeShockwave();
                useShockwaveNext = !useShockwaveNext;
                return;
            }

            if (distance <= attackRange && facingTarget >= 0.9f && Time.time >= nextAttackAt)
            {
                BeginBasicAttack();
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

        void BeginBasicAttack()
        {
            attackMode = AttackMode.Basic;
            attackLanded = false;
            attackElapsed = 0f;
            Play("Turtle_Attack", 0.06f, true);
        }

        void BeginShellSpin()
        {
            attackMode = AttackMode.ShellSpin;
            attackLanded = false;
            attackElapsed = 0f;
            activeTelegraph = CreateRing(transform.position, spinHitRadius, new Color(0.2f, 1f, 0.55f, 0.9f), 0.22f);
            Play("Turtle_Attack", 0.05f, true);
        }

        void BeginJadeShockwave()
        {
            attackMode = AttackMode.JadeShockwave;
            attackLanded = false;
            attackElapsed = 0f;
            activeTelegraph = CreateRing(transform.position, shockwaveRadius, new Color(0.2f, 1f, 0.65f, 0.9f), 0.18f);
            Play("Turtle_Attack", 0.05f, true);
        }

        void TickAttack(float distance, float facingTarget, Vector3 toPlayer)
        {
            attackElapsed += Time.deltaTime;
            switch (attackMode)
            {
                case AttackMode.Basic:
                    TickBasicAttack(distance, facingTarget);
                    break;
                case AttackMode.ShellSpin:
                    TickShellSpin(toPlayer);
                    break;
                case AttackMode.JadeShockwave:
                    TickJadeShockwave();
                    break;
            }
        }

        void TickBasicAttack(float distance, float facingTarget)
        {
            if (!attackLanded && attackElapsed >= attackWindup)
            {
                attackLanded = true;
                if (distance <= attackRange + 0.75f && facingTarget >= 0.6f)
                    DamagePlayer(attackDamage, 1.2f, 0.045f);
            }

            if (attackElapsed >= attackDuration) FinishAttack(false);
        }

        void TickShellSpin(Vector3 toPlayer)
        {
            if (attackElapsed < spinWindup) return;
            DestroyTelegraph();
            transform.Rotate(0f, 900f * Time.deltaTime, 0f, Space.World);

            float distance = toPlayer.magnitude;
            if (distance > 0.01f)
            {
                float travel = Mathf.Min(spinMoveSpeed * Time.deltaTime, Mathf.Max(0f, distance - 1.6f));
                transform.position += toPlayer.normalized * travel;
            }

            if (!attackLanded && distance <= spinHitRadius)
            {
                attackLanded = true;
                DamagePlayer(spinDamage, 2.2f, 0.075f);
            }

            if (attackElapsed >= spinDuration) FinishAttack(true);
        }

        void TickJadeShockwave()
        {
            if (!attackLanded && attackElapsed >= shockwaveWindup)
            {
                attackLanded = true;
                Vector3 impactPosition = transform.position;
                DestroyTelegraph();
                StartCoroutine(ExpandImpactRing(impactPosition, shockwaveRadius));
                if (Flat(player.transform.position - impactPosition).magnitude <= shockwaveRadius)
                    DamagePlayer(shockwaveDamage, 2.8f, 0.09f);
                else
                    player.Shake(0.65f);
            }

            if (attackElapsed >= shockwaveDuration) FinishAttack(true);
        }

        void FinishAttack(bool usedSpecial)
        {
            attackMode = AttackMode.None;
            attackLanded = false;
            DestroyTelegraph();
            nextAttackAt = Time.time + attackCooldown;
            if (usedSpecial) nextSpecialAt = Time.time + specialCooldown;
            Play("Turtle_Move", 0.1f);
        }

        void DamagePlayer(float damage, float shakeStrength, float hitStopDuration)
        {
            Vector3 point = player.transform.position + Vector3.up;
            if (!player.Health.TakeDamage(new DamageInfo(damage, gameObject, point, true))) return;
            FloatingText.Spawn(point + Vector3.up, Mathf.RoundToInt(damage).ToString(), new Color(0.25f, 1f, 0.55f));
            player.Shake(shakeStrength);
            player.HitStop(hitStopDuration);
        }

        GameObject CreateRing(Vector3 position, float radius, Color color, float width)
        {
            GameObject root = new GameObject("Rua9Chan_Ring_Runtime");
            root.transform.position = position + Vector3.up * 0.08f;
            spawnedEffects.Add(root);
            LineRenderer line = root.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 48;
            line.startWidth = width;
            line.endWidth = width;
            line.startColor = color;
            line.endColor = color;
            if (telegraphMaterial != null) line.sharedMaterial = telegraphMaterial;
            SetRingRadius(line, radius);
            return root;
        }

        IEnumerator ExpandImpactRing(Vector3 position, float radius)
        {
            GameObject effect = CreateRing(position, 0.2f, new Color(0.4f, 1f, 0.72f, 1f), 0.34f);
            LineRenderer line = effect.GetComponent<LineRenderer>();
            const float duration = 0.38f;
            float elapsed = 0f;
            while (elapsed < duration && effect != null)
            {
                elapsed += Time.unscaledDeltaTime;
                SetRingRadius(line, Mathf.Lerp(0.2f, radius, Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }
            spawnedEffects.Remove(effect);
            if (effect != null) Destroy(effect);
        }

        static void SetRingRadius(LineRenderer line, float radius)
        {
            for (int i = 0; i < line.positionCount; i++)
            {
                float angle = Mathf.PI * 2f * i / line.positionCount;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }
        }

        void DestroyTelegraph()
        {
            if (activeTelegraph == null) return;
            spawnedEffects.Remove(activeTelegraph);
            Destroy(activeTelegraph);
            activeTelegraph = null;
        }

        void CleanupEffects()
        {
            StopAllCoroutines();
            for (int i = spawnedEffects.Count - 1; i >= 0; i--)
                if (spawnedEffects[i] != null) Destroy(spawnedEffects[i]);
            spawnedEffects.Clear();
            activeTelegraph = null;
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
            attackMode = AttackMode.None;
            CleanupEffects();
            Play("Turtle_Defeated", 0.08f, true);
        }

        public void SetBattleEnded()
        {
            battleEnded = true;
            attackMode = AttackMode.None;
            CleanupEffects();
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

        static Vector3 Flat(Vector3 value)
        {
            value.y = 0f;
            return value;
        }
    }
}
