using System.Collections;
using System.Collections.Generic;
using SonTinhThuyTinh.Player;
using UnityEngine;

namespace SonTinhThuyTinh.Combat
{
    // Arena AI for the nine-tusk elephant. Besides its normal tusk swing, it can
    // telegraph a ground slam or lock a direction and charge across the arena.
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(CapsuleCollider))]
    [DisallowMultipleComponent]
    public class VoiChinNgaBoss : MonoBehaviour, IHitReceiver
    {
        enum AttackMode
        {
            None,
            Basic,
            GroundSlam,
            Charge
        }

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [Header("Movement")]
        [SerializeField, Min(0f)] float moveSpeed = 2.8f;
        [SerializeField, Min(1f)] float turnSpeed = 130f;
        [SerializeField, Min(0f)] float stopDistance = 4.7f;
        [SerializeField, Min(1f)] float arenaRadius = 19f;

        [Header("Basic Attack")]
        [SerializeField, Min(0f)] float attackRange = 6.2f;
        [SerializeField, Min(0f)] float attackDamage = 18f;
        [SerializeField, Min(0f)] float attackWindup = 0.58f;
        [SerializeField, Min(0.01f)] float attackDuration = 1.15f;
        [SerializeField, Min(0f)] float attackCooldown = 1.1f;

        [Header("Special Attacks")]
        [SerializeField, Min(0f)] float specialOpeningDelay = 2.5f;
        [SerializeField, Min(0f)] float specialCooldown = 5.2f;
        [SerializeField, Min(1f)] float slamRadius = 8.2f;
        [SerializeField, Min(0f)] float slamDamage = 25f;
        [SerializeField, Min(0.1f)] float slamWindup = 1.05f;
        [SerializeField, Min(0.1f)] float slamDuration = 1.55f;
        [SerializeField, Min(1f)] float chargeMinRange = 7f;
        [SerializeField, Min(1f)] float chargeMaxRange = 18f;
        [SerializeField, Min(0f)] float chargeDamage = 30f;
        [SerializeField, Min(0.1f)] float chargeWindup = 0.85f;
        [SerializeField, Min(0.1f)] float chargeDuration = 1.35f;
        [SerializeField, Min(0f)] float chargeSpeed = 11.5f;
        [SerializeField, Min(0.1f)] float chargeHitRadius = 2.8f;

        Animator animator;
        Health health;
        PlayerController player;
        Renderer[] renderers;
        Transform visualRoot;
        MaterialPropertyBlock propertyBlock;
        Material telegraphMaterial;
        readonly List<GameObject> spawnedEffects = new List<GameObject>();
        Color[] baseColors;
        Vector3 chargeDirection;
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

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("HDRP/Unlit")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Unlit/Color");
            if (shader != null) telegraphMaterial = new Material(shader) { name = "VoiChinNga_Telegraph_Runtime" };

            SnapVisualToGround();
            health.Died += OnDied;
        }

        void Start()
        {
            player = FindFirstObjectByType<PlayerController>();
            nextSpecialAt = Time.time + specialOpeningDelay;
            Play("Idle", 0f);
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
            if (attackMode != AttackMode.Charge && toPlayer.sqrMagnitude > 0.01f)
            {
                Quaternion facing = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, turnSpeed * Time.deltaTime);
                facingTarget = Vector3.Dot(transform.forward, toPlayer.normalized);
            }

            if (attackMode != AttackMode.None)
            {
                TickAttack(distance);
                return;
            }

            if (Time.time >= nextSpecialAt && facingTarget >= 0.8f)
            {
                if (distance >= chargeMinRange && distance <= chargeMaxRange)
                {
                    BeginCharge(toPlayer.normalized);
                    return;
                }

                if (distance <= slamRadius)
                {
                    BeginGroundSlam();
                    return;
                }
            }

            if (distance <= attackRange && facingTarget >= 0.8f && Time.time >= nextAttackAt)
            {
                BeginBasicAttack();
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

        void BeginBasicAttack()
        {
            attackMode = AttackMode.Basic;
            attackLanded = false;
            attackElapsed = 0f;
            Play("Attack", 0.08f, true);
        }

        void BeginGroundSlam()
        {
            attackMode = AttackMode.GroundSlam;
            attackLanded = false;
            attackElapsed = 0f;
            activeTelegraph = CreateRing(transform.position, slamRadius, new Color(1f, 0.25f, 0.05f, 0.9f), 0.18f);
            Play("Attack", 0.06f, true);
        }

        void BeginCharge(Vector3 direction)
        {
            attackMode = AttackMode.Charge;
            attackLanded = false;
            attackElapsed = 0f;
            chargeDirection = direction.sqrMagnitude > 0.01f ? direction : transform.forward;
            activeTelegraph = CreateChargeLine(transform.position, chargeDirection, chargeMaxRange);
            Play("Attack", 0.06f, true);
        }

        void TickAttack(float distance)
        {
            attackElapsed += Time.deltaTime;
            switch (attackMode)
            {
                case AttackMode.Basic:
                    TickBasicAttack(distance);
                    break;
                case AttackMode.GroundSlam:
                    TickGroundSlam();
                    break;
                case AttackMode.Charge:
                    TickCharge();
                    break;
            }
        }

        void TickBasicAttack(float distance)
        {
            if (!attackLanded && attackElapsed >= attackWindup)
            {
                attackLanded = true;
                Vector3 direction = Flat(player.transform.position - transform.position).normalized;
                float facing = Vector3.Dot(transform.forward, direction);
                if (distance <= attackRange + 0.8f && facing > 0.15f)
                    DamagePlayer(attackDamage, 1.2f, 0.045f);
            }

            if (attackElapsed >= attackDuration) FinishAttack(false);
        }

        void TickGroundSlam()
        {
            if (!attackLanded && attackElapsed >= slamWindup)
            {
                attackLanded = true;
                Vector3 impactPosition = transform.position;
                DestroyTelegraph();
                StartCoroutine(ExpandImpactRing(impactPosition, slamRadius));

                if (Flat(player.transform.position - impactPosition).magnitude <= slamRadius)
                    DamagePlayer(slamDamage, 2.4f, 0.075f);
                else
                    player.Shake(0.7f);
            }

            if (attackElapsed >= slamDuration) FinishAttack(true);
        }

        void TickCharge()
        {
            if (attackElapsed < chargeWindup) return;

            DestroyTelegraph();
            Play("Walk_Forward", 0.05f);
            if (animator != null) animator.speed = 1.7f;

            Vector3 next = transform.position + chargeDirection * (chargeSpeed * Time.deltaTime);
            Vector3 planar = Flat(next);
            if (planar.magnitude > arenaRadius)
            {
                planar = planar.normalized * arenaRadius;
                next = new Vector3(planar.x, transform.position.y, planar.z);
            }
            transform.position = next;

            if (!attackLanded && Flat(player.transform.position - transform.position).magnitude <= chargeHitRadius)
            {
                attackLanded = true;
                DamagePlayer(chargeDamage, 3.2f, 0.1f);
            }

            if (attackElapsed >= chargeDuration) FinishAttack(true);
        }

        void FinishAttack(bool usedSpecial)
        {
            attackMode = AttackMode.None;
            attackLanded = false;
            DestroyTelegraph();
            if (animator != null) animator.speed = 1f;
            nextAttackAt = Time.time + attackCooldown;
            if (usedSpecial) nextSpecialAt = Time.time + specialCooldown;
            Play("Idle", 0.1f);
        }

        void DamagePlayer(float damage, float shakeStrength, float hitStopDuration)
        {
            Vector3 point = player.transform.position + Vector3.up;
            if (!player.Health.TakeDamage(new DamageInfo(damage, gameObject, point, true))) return;
            FloatingText.Spawn(point + Vector3.up, Mathf.RoundToInt(damage).ToString(), new Color(1f, 0.35f, 0.2f));
            player.Shake(shakeStrength);
            player.HitStop(hitStopDuration);
        }

        GameObject CreateRing(Vector3 position, float radius, Color color, float width)
        {
            GameObject root = new GameObject("VoiChinNga_Ring_Runtime");
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
            for (int i = 0; i < line.positionCount; i++)
            {
                float angle = Mathf.PI * 2f * i / line.positionCount;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }
            return root;
        }

        GameObject CreateChargeLine(Vector3 position, Vector3 direction, float length)
        {
            GameObject root = new GameObject("VoiChinNga_ChargeWarning_Runtime");
            root.transform.position = position + Vector3.up * 0.1f;
            spawnedEffects.Add(root);
            LineRenderer line = root.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.startWidth = chargeHitRadius * 2f;
            line.endWidth = chargeHitRadius * 2f;
            line.startColor = new Color(1f, 0.2f, 0.05f, 0.35f);
            line.endColor = new Color(1f, 0.65f, 0.05f, 0.75f);
            if (telegraphMaterial != null) line.sharedMaterial = telegraphMaterial;
            line.SetPosition(0, Vector3.zero);
            line.SetPosition(1, direction * length);
            return root;
        }

        IEnumerator ExpandImpactRing(Vector3 position, float radius)
        {
            GameObject effect = CreateRing(position, 0.2f, new Color(1f, 0.75f, 0.1f, 1f), 0.35f);
            LineRenderer line = effect.GetComponent<LineRenderer>();
            const float duration = 0.32f;
            float elapsed = 0f;
            while (elapsed < duration && effect != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float currentRadius = Mathf.Lerp(0.2f, radius, Mathf.Clamp01(elapsed / duration));
                for (int i = 0; i < line.positionCount; i++)
                {
                    float angle = Mathf.PI * 2f * i / line.positionCount;
                    line.SetPosition(i, new Vector3(Mathf.Cos(angle) * currentRadius, 0f, Mathf.Sin(angle) * currentRadius));
                }
                yield return null;
            }
            spawnedEffects.Remove(effect);
            if (effect != null) Destroy(effect);
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

            if (renderers == null || renderers.Length == 0) return;
            float lowest = renderers[0].bounds.min.y;
            for (int i = 1; i < renderers.Length; i++) lowest = Mathf.Min(lowest, renderers[i].bounds.min.y);
            visualRoot.position += Vector3.up * (transform.position.y - lowest + 0.01f);
        }

        void OnDied(DamageInfo _)
        {
            battleEnded = true;
            attackMode = AttackMode.None;
            CleanupEffects();
            if (animator != null) animator.speed = 1f;
            Play("Death", 0.08f);
        }

        public void SetBattleEnded()
        {
            battleEnded = true;
            attackMode = AttackMode.None;
            CleanupEffects();
            if (animator != null) animator.speed = 1f;
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

        static Vector3 Flat(Vector3 value)
        {
            value.y = 0f;
            return value;
        }
    }
}
