using System.Collections;
using System.Collections.Generic;
using SonTinhThuyTinh.Audio;
using SonTinhThuyTinh.Player;
using UnityEngine;

namespace SonTinhThuyTinh.Combat
{
    // Arena AI for the nine-tusk elephant. Besides its normal tusk swing, it can
    // slam the ground, charge across the arena, or scoop up and throw a rock.
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
            Charge,
            RockThrow
        }

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly Color TelegraphRed = new Color(1f, 0.06f, 0.03f, 1f);

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
        [SerializeField, Min(0.01f)] float chargeBurstTime = 0.16f;
        [SerializeField, Min(0.1f)] float chargeHitRadius = 2.8f;

        [Header("Rock Throw")]
        [SerializeField, Min(1f)] float rockMinRange = 8.5f;
        [SerializeField, Min(1f)] float rockMaxRange = 24f;
        [SerializeField, Min(0f)] float rockDamage = 22f;
        [SerializeField, Min(0.1f)] float rockWindup = 1.05f;
        [SerializeField, Min(0.1f)] float rockScoopTime = 0.72f;
        [SerializeField, Min(0.1f)] float rockFlightTime = 0.85f;
        [SerializeField, Min(0f)] float rockRecovery = 0.5f;
        [SerializeField, Min(0.5f)] float rockImpactRadius = 2.7f;
        [SerializeField, Min(0.5f)] float rockArcHeight = 5.5f;
        [SerializeField, Min(0f)] float rockVolleySpread = 2.2f;

        Animator animator;
        Health health;
        PlayerController player;
        Renderer[] renderers;
        Transform visualRoot;
        MaterialPropertyBlock propertyBlock;
        Material telegraphMaterial;
        readonly List<GameObject> spawnedEffects = new List<GameObject>();
        readonly List<ElephantRockProjectile> activeRocks = new List<ElephantRockProjectile>(5);
        readonly List<Transform> footBones = new List<Transform>(4);
        readonly RaycastHit[] groundProbeHits = new RaycastHit[16];
        Color[] baseColors;
        Vector3 chargeDirection;
        Vector3 rockTarget;
        // The arena is centred on the world origin in Battle_VoiChinNga; the Map_SonTinh arena sets its own centre (SetArena).
        Vector3 arenaCentre;
        GameObject activeTelegraph;
        AttackMode attackMode;
        float attackElapsed;
        float nextAttackAt;
        float nextSpecialAt;
        float flash;
        float slowFactor = 1f;
        float slowUntil;
        int requestedState;
        int specialPattern;
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
            if (shader != null)
            {
                telegraphMaterial = new Material(shader) { name = "VoiChinNga_Telegraph_Runtime" };
                // URP's Unlit ignores the LineRenderer's own colours (the rings came out white): the material carries the red
                if (telegraphMaterial.HasProperty(BaseColor)) telegraphMaterial.SetColor(BaseColor, TelegraphRed);
                else if (telegraphMaterial.HasProperty("_Color")) telegraphMaterial.SetColor("_Color", TelegraphRed);
            }

            CacheFootBones();
            AlignFeetToGround();
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
                bool canThrowRock = distance >= rockMinRange && distance <= rockMaxRange;
                bool canCharge = distance >= chargeMinRange && distance <= chargeMaxRange;

                // At medium range, alternate the charge and the rock throw. Beyond
                // charge range the rock gives the elephant a readable ranged answer.
                if (canThrowRock && (!canCharge || specialPattern % 2 == 0))
                {
                    specialPattern++;
                    BeginRockThrow();
                    return;
                }

                if (canCharge)
                {
                    specialPattern++;
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

        // Animator evaluates after Update. Keep the lowest animated foot on the
        // arena plane so imported clips can never drag the elephant underground.
        void LateUpdate()
        {
            if (visualRoot == null || health == null || health.IsDead || battleEnded) return;
            // Charge movement is driven by the boss root. Correcting the animated
            // feet every frame here makes the visual root fight that movement and jitter.
            if (attackMode == AttackMode.Charge) return;
            AlignFeetToGround();
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
            Play("Attack", 0.06f, true);
        }

        void BeginCharge(Vector3 direction)
        {
            // Ground once before the burst, then leave the visual root untouched
            // until the charge ends so there is only one source of movement.
            AlignFeetToGround();
            attackMode = AttackMode.Charge;
            attackLanded = false;
            attackElapsed = 0f;
            chargeDirection = direction.sqrMagnitude > 0.01f ? direction : transform.forward;
            Play("Attack", 0.06f, true);
        }

        void BeginRockThrow()
        {
            attackMode = AttackMode.RockThrow;
            attackLanded = false;
            attackElapsed = 0f;

            rockTarget = ClampRockTarget(player.transform.position);
            activeRocks.Clear();

            Vector3 targetRight = Flat(transform.right).normalized * rockVolleySpread;
            Vector3 targetForward = Flat(transform.forward).normalized * rockVolleySpread;
            Vector3[] targetOffsets =
            {
                Vector3.zero,
                -targetRight,
                targetRight,
                targetForward,
                -targetForward
            };
            Vector3[] carryOffsets =
            {
                Vector3.zero,
                new Vector3(-0.68f, 0.12f, -0.18f),
                new Vector3(0.68f, 0.12f, -0.18f),
                new Vector3(-0.34f, 0.62f, -0.28f),
                new Vector3(0.34f, 0.62f, -0.28f)
            };

            for (int i = 0; i < 5; i++)
            {
                Vector3 target = ClampRockTarget(rockTarget + targetOffsets[i]);
                GameObject rockObject = ElephantRockProjectile.Create(
                    transform, target, carryOffsets[i], rockScoopTime,
                    rockFlightTime, rockArcHeight, OnRockImpact);
                activeRocks.Add(rockObject.GetComponent<ElephantRockProjectile>());
                spawnedEffects.Add(rockObject);
            }
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
                case AttackMode.RockThrow:
                    TickRockThrow();
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
                GameObject fracture = ElephantStompVfx.Spawn(impactPosition, slamRadius);
                spawnedEffects.Add(fracture);
                GameAudio.Play(Sfx.ElephantStomp, impactPosition, 1.15f);

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
            float travelTime = attackElapsed - chargeWindup;

            // Use an in-place clip for the whole burst. Charge_Burst contains imported
            // translation, which doubled up with the scripted movement and looked like a teleport.
            float burst = Mathf.Clamp01(travelTime / chargeBurstTime);
            burst = burst * burst * (3f - 2f * burst);
            Play("Walk_Forward", 0.08f);
            if (animator != null) animator.speed = Mathf.Lerp(1.25f, 2.15f, burst);

            // A long frame must not move several frames' worth of distance at once.
            // Capping only the movement delta keeps the charge readable under frame spikes.
            float movementDelta = Mathf.Min(Time.deltaTime, 1f / 30f);
            Vector3 previous = transform.position;
            Vector3 next = previous + chargeDirection * (chargeSpeed * burst * movementDelta);
            Vector3 planar = Flat(next - arenaCentre);
            if (planar.magnitude > arenaRadius)
            {
                planar = planar.normalized * arenaRadius;
                next = new Vector3(arenaCentre.x + planar.x, transform.position.y, arenaCentre.z + planar.z);
            }
            transform.position = next;

            // Sweep the whole travelled segment so a fast charge cannot tunnel through Sơn Tinh.
            if (!attackLanded && DistanceToSegment(
                    Flat(player.transform.position), Flat(previous), Flat(next)) <= chargeHitRadius)
            {
                attackLanded = true;
                DamagePlayer(chargeDamage, 3.2f, 0.1f);
            }

            // chargeDuration is active travel time; the warning wind-up happens before it.
            if (travelTime >= chargeDuration) FinishAttack(true);
        }

        void TickRockThrow()
        {
            if (!attackLanded && attackElapsed >= rockWindup)
            {
                attackLanded = true;
                foreach (ElephantRockProjectile rock in activeRocks)
                    if (rock != null) rock.Launch();
                activeRocks.Clear();
                GameAudio.Play(Sfx.SwingHeavy, transform.position + transform.forward * 3f, 0.9f);
            }

            if (attackElapsed >= rockWindup + rockFlightTime + rockRecovery)
                FinishAttack(true);
        }

        void OnRockImpact(Vector3 impactPosition)
        {
            if (battleEnded || health == null || health.IsDead) return;

            StartCoroutine(ExpandImpactRing(impactPosition, rockImpactRadius));
            GameObject fracture = ElephantStompVfx.Spawn(impactPosition, rockImpactRadius * 0.85f);
            spawnedEffects.Add(fracture);
            GameAudio.Play(Sfx.HitHeavy, impactPosition, 1.05f);

            if (player != null && !player.Health.IsDead &&
                Flat(player.transform.position - impactPosition).magnitude <= rockImpactRadius)
                DamagePlayer(rockDamage, 2.8f, 0.09f);
            else if (player != null)
                player.Shake(0.55f);
        }

        void FinishAttack(bool usedSpecial)
        {
            bool finishedCharge = attackMode == AttackMode.Charge;
            attackMode = AttackMode.None;
            attackLanded = false;
            DestroyTelegraph();
            if (animator != null) animator.speed = 1f;
            if (finishedCharge) AlignFeetToGround();
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

            direction = Flat(direction).normalized;
            Vector3 side = new Vector3(-direction.z, 0f, direction.x) * chargeHitRadius;
            float startDistance = Mathf.Min(1.4f, length * 0.12f);
            Vector3 start = direction * startDistance;
            Vector3 end = direction * length;

            // Two thin rails describe the dangerous lane without covering the floor.
            CreateTelegraphLine(root.transform, "ChargeBoundary_Left",
                new[] { start + side, end + side }, 0.16f);
            CreateTelegraphLine(root.transform, "ChargeBoundary_Right",
                new[] { start - side, end - side }, 0.16f);

            // A compact arrowhead keeps the locked charge direction readable at a glance.
            float arrowLength = Mathf.Min(2.2f, length * 0.18f);
            float arrowHalfWidth = Mathf.Min(chargeHitRadius * 0.68f, 1.8f);
            CreateTelegraphLine(root.transform, "ChargeArrow",
                new[]
                {
                    end - direction * arrowLength + new Vector3(-direction.z, 0f, direction.x) * arrowHalfWidth,
                    end,
                    end - direction * arrowLength - new Vector3(-direction.z, 0f, direction.x) * arrowHalfWidth
                }, 0.22f);

            return root;
        }

        LineRenderer CreateTelegraphLine(Transform parent, string lineName, Vector3[] points, float width)
        {
            GameObject lineObject = new GameObject(lineName);
            lineObject.transform.SetParent(parent, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = points.Length;
            line.startWidth = width;
            line.endWidth = width;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.startColor = TelegraphRed;
            line.endColor = TelegraphRed;
            if (telegraphMaterial != null) line.sharedMaterial = telegraphMaterial;
            line.SetPositions(points);
            return line;
        }

        IEnumerator ExpandImpactRing(Vector3 position, float radius)
        {
            GameObject effect = CreateRing(position, 0.2f, new Color(1f, 0.12f, 0.05f, 1f), 0.35f);
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
            activeRocks.Clear();
        }

        Vector3 ClampRockTarget(Vector3 target)
        {
            Vector3 fromCentre = Flat(target - arenaCentre);
            float targetLimit = Mathf.Max(1f, arenaRadius - rockImpactRadius);
            if (fromCentre.magnitude > targetLimit)
                fromCentre = fromCentre.normalized * targetLimit;

            // Both elephant arenas have a flat fighting floor.
            return new Vector3(arenaCentre.x + fromCentre.x,
                transform.position.y + 0.05f, arenaCentre.z + fromCentre.z);
        }

        void Play(string state, float fade, bool restart = false)
        {
            if (animator == null) return;
            int stateHash = Animator.StringToHash(state);
            if (!restart && requestedState == stateHash) return;
            requestedState = stateHash;
            animator.CrossFadeInFixedTime(stateHash, fade, 0, 0f);
        }

        void CacheFootBones()
        {
            footBones.Clear();
            if (visualRoot == null) return;

            Transform[] bones = visualRoot.GetComponentsInChildren<Transform>(true);
            foreach (Transform bone in bones)
                if (bone.name.EndsWith(".foot", System.StringComparison.OrdinalIgnoreCase))
                    footBones.Add(bone);
        }

        // Places the actual animated foot bones on the colliders below them. This is
        // public because the arena deliberately disables the AI during its intro.
        public void AlignFeetToGround()
        {
            if (visualRoot == null) return;

            if (footBones.Count == 0) CacheFootBones();
            float soleBelowAnkle = 0.04f * Mathf.Abs(visualRoot.lossyScale.x);
            float highestRequiredLift = float.NegativeInfinity;
            foreach (Transform foot in footBones)
            {
                if (foot == null || !TryGroundHeightBelow(foot.position, out float groundY)) continue;
                float requiredLift = groundY + soleBelowAnkle + 0.01f - foot.position.y;
                highestRequiredLift = Mathf.Max(highestRequiredLift, requiredLift);
            }

            if (!float.IsNegativeInfinity(highestRequiredLift))
            {
                visualRoot.position += Vector3.up * highestRequiredLift;
                return;
            }

            // Fallback for scenes whose floor has no collider.
            float lowestAnkle = float.PositiveInfinity;
            foreach (Transform foot in footBones)
                if (foot != null) lowestAnkle = Mathf.Min(lowestAnkle, foot.position.y);
            if (!float.IsPositiveInfinity(lowestAnkle))
            {
                visualRoot.position += Vector3.up *
                    (transform.position.y + soleBelowAnkle + 0.01f - lowestAnkle);
                return;
            }

            if (renderers == null || renderers.Length == 0) return;
            float lowest = renderers[0].bounds.min.y;
            for (int i = 1; i < renderers.Length; i++) lowest = Mathf.Min(lowest, renderers[i].bounds.min.y);
            visualRoot.position += Vector3.up * (transform.position.y - lowest + 0.01f);
        }

        bool TryGroundHeightBelow(Vector3 footPosition, out float groundY)
        {
            const float probeHeight = 8f;
            const float probeDistance = 20f;
            Vector3 origin = footPosition + Vector3.up * probeHeight;
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, groundProbeHits,
                probeDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            groundY = float.NegativeInfinity;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = groundProbeHits[i];
                if (hit.collider == null || hit.normal.y < 0.35f) continue;
                Transform hitTransform = hit.collider.transform;
                if (hitTransform == transform || hitTransform.IsChildOf(transform)) continue;
                groundY = Mathf.Max(groundY, hit.point.y);
            }
            return !float.IsNegativeInfinity(groundY);
        }

        void OnDied(DamageInfo _)
        {
            battleEnded = true;
            attackMode = AttackMode.None;
            CleanupEffects();
            if (animator != null) animator.speed = 1f;
            Play("Death", 0.08f);
        }

        // Centre and radius of the fighting floor (the charge stops at the edge of it).
        public void SetArena(Vector3 centre, float radius)
        {
            arenaCentre = Flat(centre);
            arenaRadius = Mathf.Max(1f, radius);
        }

        // Back to the start of the fight after the player lost: attacks cleared, the opening delay again. The caller refills the health.
        public void ResetForRetry()
        {
            battleEnded = false;
            attackMode = AttackMode.None;
            attackLanded = false;
            CleanupEffects();
            if (animator != null) animator.speed = 1f;
            nextAttackAt = Time.time + 1.5f;
            nextSpecialAt = Time.time + specialOpeningDelay;
            Play("Idle", 0.1f, true);
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

        static float DistanceToSegment(Vector3 point, Vector3 start, Vector3 end)
        {
            Vector3 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= 0.0001f) return Vector3.Distance(point, start);
            float amount = Mathf.Clamp01(Vector3.Dot(point - start, segment) / lengthSquared);
            return Vector3.Distance(point, start + segment * amount);
        }
    }
}
