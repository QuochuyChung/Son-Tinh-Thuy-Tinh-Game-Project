using SonTinhThuyTinh.Player;
using UnityEngine;

namespace SonTinhThuyTinh.Combat
{
    // Drives a rival character as a boss: it presses the same buffered inputs the keyboard would,
    // so every attack, spell and locomotion rule stays in the main game's state machine (PlayerController).
    // Any character prefab with PlayerController + PlayerInputReader can be a boss — just add this component.
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

        PlayerController player;
        PlayerInputReader input;
        PlayerController targetPlayer;
        bool active;
        float nextAttackAt;
        float nextSpellAt;

        void Awake()
        {
            player = GetComponent<PlayerController>();
            input = GetComponent<PlayerInputReader>();
        }

        // Called right after the boss is instantiated (BossArenaDuel / SandboxBossSpawner).
        // A null who falls back to the scene's PlayerSpawner; with no target at all the boss just idles under AI control.
        public void Begin(Transform who)
        {
            if (input == null) input = GetComponent<PlayerInputReader>();

            target = who != null ? who : FindScenePlayer();
            targetPlayer = target != null ? target.GetComponentInParent<PlayerController>() : null;
            if (target == null)
                Debug.LogWarning($"BossAI ({name}): no target found; the boss will wait.", this);

            active = true;
            input.AiActivate();
        }

        // Hands the controls back to the keyboard (or tears down on destroy).
        public void Stop()
        {
            if (!active) return;
            active = false;
            if (input == null) return;
            input.AiSetLocomotion(Vector2.zero, false);
            input.AiDeactivate();
        }

        void OnDisable() => Stop();

        void Update()
        {
            if (!active) return;

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

            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;
            float dist = toTarget.magnitude;

            if (dist > stopDistance)
            {
                Vector3 dir = dist > 0.0001f ? toTarget / dist : Vector3.zero;
                input.AiSetLocomotion(ToStick(dir), dist > sprintDistance);
            }
            else
            {
                input.AiSetLocomotion(Vector2.zero, false);
                // With no stick Locomote stops turning, so track the target by hand.
                if (dist > 0.5f) player.FaceTowards(toTarget, 720f * Time.deltaTime);
            }

            Fight(dist);
        }

        // Light presses chain into combos while the target stays in reach; spells wait for their own band.
        void Fight(float dist)
        {
            if (!player.HasCombat) return;

            if (dist <= attackRange && Time.time >= nextAttackAt)
            {
                input.AiPressLight();
                nextAttackAt = Time.time + attackInterval;
            }

            if (dist < spellMinRange || dist > spellMaxRange || Time.time < nextSpellAt) return;

            for (int i = 0; i < 3; i++)
                if (player.SpellReady(i))
                {
                    input.AiPressSpell(i);
                    nextSpellAt = Time.time + spellGate;
                    break;
                }
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
