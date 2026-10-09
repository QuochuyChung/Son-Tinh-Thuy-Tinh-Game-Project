using SonTinhThuyTinh.Player;
using UnityEngine;

namespace SonTinhThuyTinh.Combat
{
    // Drives a rival character as a boss: it presses the same buffered inputs the keyboard would,
    // so every attack, spell and locomotion rule stays in the main game's state machine (PlayerController).
    // Any character prefab with PlayerController + PlayerInputReader can be a boss — just add this component.
    //
    // Two-phase pattern (SetPhase, called by BossArenaDuel at <=50% HP):
    //   Phase 1 — measured: light combos in bursts with a heavy finisher and a rest between bursts,
    //             contextual spell pick by SpellKind + distance, backs off after a big hit.
    //   Phase 2 — cuồng nộ: tighter cadence, shorter spell gate, stays glued to the player, and the
    //             signature projectile -> sprint-into-melee rush. Patterns key on SpellKind (not character),
    //             so Thủy Tinh and Sơn Tinh behave identically without per-character code.
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(PlayerInputReader))]
    public class BossAI : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("Who the boss chases; found via PlayerSpawner when left empty.")]
        [SerializeField] Transform target;

        [Header("Approach")]
        [Tooltip("Stop here and face the target; closer than this is attack range.")]
        [SerializeField] float stopDistance = 1.9f;
        [Tooltip("Sprint instead of run beyond this distance.")]
        [SerializeField] float sprintDistance = 8f;
        [Tooltip("How close the target must be for light attacks.")]
        [SerializeField] float attackRange = 2.6f;

        [Header("Attacks")]
        [Tooltip("Minimum seconds between light attacks (combos keep chaining inside the input buffer).")]
        [SerializeField] float attackInterval = 0.15f;
        [Tooltip("Spells only fire between these distances, so they never compete with melee presses.")]
        [SerializeField] float spellMinRange = 4f;
        [SerializeField] float spellMaxRange = 12f;
        [Tooltip("Minimum seconds between spell attempts; each slot's own cooldown still applies.")]
        [SerializeField] float spellGate = 3f;

        [Header("Phase 1 pacing")]
        [Tooltip("Light hits per burst before the boss pauses (last hit may come out as a heavy finisher).")]
        [SerializeField] int burstHits = 3;
        [Tooltip("Seconds of not attacking between bursts (random range).")]
        [SerializeField] Vector2 burstRest = new(0.6f, 1.0f);
        [Tooltip("A hit this big (fraction of max HP) makes the boss back off for a moment.")]
        [SerializeField, Range(0.05f, 0.5f)] float bigHitThreshold = 0.15f;
        [SerializeField] float backOffDuration = 1.2f;

        [Header("Phase 2 (Cuồng nộ)")]
        [Tooltip("Seconds between light attacks once phase 2 starts.")]
        [SerializeField] float attackIntervalPhase2 = 0.1f;
        [Tooltip("Seconds between spell attempts once phase 2 starts.")]
        [SerializeField] float spellGatePhase2 = 1.5f;
        [Tooltip("Sprint distance once phase 2 starts; the boss stays glued to the player.")]
        [SerializeField] float sprintDistancePhase2 = 5f;
        [Tooltip("Seconds between bursts once phase 2 starts.")]
        [SerializeField] float burstRestPhase2 = 0.3f;
        [Tooltip("Stand-still telegraph when phase 2 begins, timed with the bar flipping to Cuồng nộ.")]
        [SerializeField] float phase2Telegraph = 0.7f;
        [Tooltip("After a phase-2 projectile the boss sprints straight back into melee for this long.")]
        [SerializeField] float rushAfterProjectile = 1.5f;

        PlayerController player;
        PlayerInputReader input;
        PlayerController targetPlayer;
        bool active;
        bool healthHooked;
        float nextAttackAt;
        float nextSpellAt;
        int phase = 1;
        int burstCount;
        float backOffUntil;
        float rushUntil;
        float telegraphUntil;

        void Awake()
        {
            player = GetComponent<PlayerController>();
            input = GetComponent<PlayerInputReader>();
        }

        public int Phase => phase;

        // Called right after the boss is instantiated (BossArenaDuel / SandboxBossSpawner).
        // A null who falls back to the scene's PlayerSpawner; with no target at all the boss just idles under AI control.
        public void Begin(Transform who)
        {
            if (input == null) input = GetComponent<PlayerInputReader>();

            target = who != null ? who : FindScenePlayer();
            targetPlayer = target != null ? target.GetComponentInParent<PlayerController>() : null;
            if (target == null)
                Debug.LogWarning($"BossAI ({name}): no target found; the boss will wait.", this);

            HookHealth();
            active = true;
            input.AiActivate();
        }

        // Hands the controls back to the keyboard (or tears down on destroy).
        public void Stop()
        {
            if (!active) return;
            active = false;
            UnhookHealth();
            if (input == null) return;
            input.AiSetLocomotion(Vector2.zero, false);
            input.AiDeactivate();
        }

        // Phase 2 trigger (BossArenaDuel at <=50% HP). Stands still for the telegraph so the
        // health bar flipping purple + dot graying and the rage pose all read as one moment.
        public void SetPhase(int newPhase)
        {
            if (newPhase <= phase) return;
            phase = newPhase;
            burstCount = 0;
            backOffUntil = 0f;
            if (phase == 2) telegraphUntil = Time.time + phase2Telegraph;
        }

        void OnDisable() => Stop();

        void Update()
        {
            if (!active) return;
            HookHealth();

            // The player despawned (scene switch): look for a replacement before doing anything else.
            if (target == null)
            {
                target = FindScenePlayer();
                targetPlayer = target != null ? target.GetComponentInParent<PlayerController>() : null;
                if (target == null)
                {
                    input.AiSetLocomotion(Vector2.zero, false);
                    return;
                }
            }

            // Dead on either side: stand still but stay engaged, so reviving (R in the editor) resumes the duel.
            if ((player.Health != null && player.Health.IsDead) ||
                (targetPlayer != null && targetPlayer.Health != null && targetPlayer.Health.IsDead))
            {
                input.AiSetLocomotion(Vector2.zero, false);
                return;
            }

            // Phase 2 telegraph: freeze until the rage moment has landed.
            if (Time.time < telegraphUntil)
            {
                input.AiSetLocomotion(Vector2.zero, false);
                return;
            }

            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;
            float dist = toTarget.magnitude;

            // Phase 1 only: back away after a big hit instead of trading in place.
            if (phase == 1 && Time.time < backOffUntil)
            {
                input.AiSetLocomotion(dist > 0.0001f ? ToStick(-toTarget / dist) : Vector2.zero, false);
                return;
            }

            if (dist > stopDistance)
            {
                Vector3 dir = dist > 0.0001f ? toTarget / dist : Vector3.zero;
                float sprintDist = phase == 2 ? sprintDistancePhase2 : sprintDistance;
                if (phase == 2 && Time.time < rushUntil) sprintDist = 0f; // signature rush: always sprint back in
                input.AiSetLocomotion(ToStick(dir), dist > sprintDist);
            }
            else
            {
                input.AiSetLocomotion(Vector2.zero, false);
                // With no stick Locomote stops turning, so track the target by hand.
                if (dist > 0.5f) player.FaceTowards(toTarget, 720f * Time.deltaTime);
            }

            Fight(dist);
        }

        // Light presses come in bursts with a heavy finisher and a rest; spells wait for their own band
        // and are picked by kind + distance instead of "first slot ready".
        void Fight(float dist)
        {
            if (!player.HasCombat) return;
            if (dist > attackRange) burstCount = 0;

            if (dist <= attackRange && Time.time >= nextAttackAt)
            {
                bool finishing = burstCount >= burstHits - 1;
                if (finishing && HeavyReady())
                {
                    input.AiPressHeavy();
                    burstCount = 0;
                    nextAttackAt = Time.time + RestFor(phase);
                }
                else
                {
                    input.AiPressLight();
                    burstCount = (burstCount + 1) % burstHits;
                    nextAttackAt = Time.time + (phase == 2 ? attackIntervalPhase2 : attackInterval);
                }
            }

            CastSpell(dist);
        }

        void CastSpell(float dist)
        {
            if (dist < spellMinRange || dist > spellMaxRange || Time.time < nextSpellAt) return;

            int slot = PickSpell(dist);
            if (slot < 0) return;

            input.AiPressSpell(slot);
            nextSpellAt = Time.time + (phase == 2 ? spellGatePhase2 : spellGate);

            // Phase 2 signature: fire the travelling projectile, then sprint straight back into melee.
            if (phase == 2 && KindOf(slot) == SpellKind.Wave)
                rushUntil = Time.time + rushAfterProjectile;
        }

        // Same distance mapping on both bosses because every moveset shares SpellKind order:
        //   far    -> pull (Wind / Núi non) to catch a runner,
        //   mid    -> travelling projectile (Wave / Núi mọc),
        //   close  -> zone (Rain / Núi dâng) right after a melee exchange.
        int PickSpell(float dist)
        {
            SpellKind want = dist > 8f ? SpellKind.Wind
                          : dist >= 6f ? SpellKind.Wave
                          : SpellKind.Rain;

            int fallback = -1;
            for (int i = 0; i < 3; i++)
            {
                if (player.MoveSet == null || player.MoveSet.spells == null || i >= player.MoveSet.spells.Length) break;
                if (!player.SpellReady(i)) continue;
                if (fallback < 0) fallback = i;
                if (KindOf(i) == want) return i;
            }
            return fallback;
        }

        SpellKind KindOf(int slot)
        {
            SpellData[] spells = player.MoveSet != null ? player.MoveSet.spells : null;
            return spells != null && slot < spells.Length && spells[slot] != null ? spells[slot].kind : SpellKind.Wind;
        }

        bool HeavyReady()
        {
            AttackData heavy = player.MoveSet != null ? player.MoveSet.heavy : null;
            if (heavy == null) return false;
            if (player.Stamina != null && heavy.staminaCost > 0f && player.Stamina.Current < heavy.staminaCost) return false;
            return true;
        }

        static float RestFor(int phase) => phase == 2 ? 0.3f : 0.8f;

        // Big hits in phase 1 trigger the back-off (phase 2 never yields ground).
        void HookHealth()
        {
            if (healthHooked || player.Health == null) return;
            player.Health.Damaged += OnBossDamaged;
            healthHooked = true;
        }

        void UnhookHealth()
        {
            if (!healthHooked || player == null || player.Health == null) return;
            player.Health.Damaged -= OnBossDamaged;
            healthHooked = false;
        }

        void OnBossDamaged(DamageInfo info)
        {
            if (phase != 1 || player.Health == null) return;
            if (info.Amount >= player.Health.Max * bigHitThreshold)
                backOffUntil = Time.time + backOffDuration;
        }

        // Inverse of PlayerController.CameraRelative: a world direction on the ground plane -> camera-relative stick.
        Vector2 ToStick(Vector3 worldDir)
        {
            Camera cam = Camera.main;
            Vector3 forward = cam != null ? cam.transform.forward : transform.forward;
            Vector3 right = cam != null ? cam.transform.right : transform.right;
            forward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            right = Vector3.ProjectOnPlane(right, Vector3.up).normalized;
            return new Vector2(Vector3.Dot(worldDir, right), Vector3.Dot(worldDir, forward));
        }

        static Transform FindScenePlayer()
        {
            PlayerSpawner spawner = FindFirstObjectByType<PlayerSpawner>();
            return spawner != null && spawner.Player != null ? spawner.Player.transform : null;
        }
    }
}
