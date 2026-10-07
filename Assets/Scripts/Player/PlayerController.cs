using SonTinhThuyTinh.Combat;
using SonTinhThuyTinh.Core;
using SonTinhThuyTinh.Player.States;
using Unity.Cinemachine;
using UnityEngine;

namespace SonTinhThuyTinh.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        static readonly int LocomotionBlendParam = Animator.StringToHash("LocomotionBlend");
        static readonly int AnimSpeedParam = Animator.StringToHash("AnimSpeed");

        [SerializeField] PlayerInputReader input;
        [SerializeField] Animator animator;
        [SerializeField] Stamina stamina;
        [SerializeField] Health health;
        [Tooltip("Point the follow camera orbits around (about head height).")]
        [SerializeField] Transform cameraTarget;
        [Tooltip("Movement is relative to this camera. Defaults to Camera.main.")]
        [SerializeField] Transform cameraTransform;

        [Header("Locomotion")]
        [Tooltip("Natural root speed (m/s) of this character's Walk clip at its current scale, so feet don't slide.")]
        [SerializeField] float walkSpeed = 2f;
        [Tooltip("Natural root speed (m/s) of this character's Run clip at its current scale. Also the top movement speed.")]
        [SerializeField] float runSpeed = 6.6f;
        [Tooltip("Natural root speed (m/s) of this character's Sprint clip at its current scale. Reached while Sprint (Shift) is held and the character is moving; otherwise the top speed is Run Speed.")]
        [SerializeField] float sprintSpeed = 9f;
        [SerializeField] float acceleration = 12f;
        [SerializeField] float deceleration = 16f;
        [Tooltip("Degrees per second.")]
        [SerializeField] float turnSpeed = 720f;
        [SerializeField] float gravity = -20f;

        [Header("Dodge")]
        [SerializeField] DodgeSettings dodge = new();

        [Header("Jump (Space; Space while sprinting = running jump)")]
        [SerializeField] JumpSettings jump = new();

        [Header("Slide (C while sprinting)")]
        [SerializeField] SlideSettings slide = new();

        [Header("Climb (Space next to a ledge; off = cannot climb)")]
        [SerializeField] ClimbSettings climb = new();

        [Header("Combat (empty = cannot fight)")]
        [SerializeField] MoveSet moveSet;
        [Tooltip("Optional: the glowing trail of the sword. Found on the sword at runtime when empty.")]
        [SerializeField] SwordTrail swordTrail;

        CharacterController body;
        readonly StateMachine stateMachine = new();
        float verticalVelocity;
        float slideReadyAt;
        float climbReadyAt;
        float hitStopUntil;
        float[] spellReadyAt = new float[3];
        CinemachineImpulseSource shakeSource;

        public PlayerInputReader InputReader => input;
        public Animator Animator => animator;
        public Stamina Stamina => stamina;
        public Health Health => health;
        public Transform CameraTarget => cameraTarget;
        public DodgeSettings Dodge => dodge;
        public JumpSettings JumpSettings => jump;
        public SlideSettings Slide => slide;
        public ClimbSettings Climb => climb;
        public float CurrentSpeed { get; private set; }
        public float RunSpeed => runSpeed;
        public float SprintSpeed => sprintSpeed;
        public bool IsGrounded => body.isGrounded;
        // Shift held and really running (not just starting to walk): what the running jump and the slide need.
        public bool IsSprinting => input.SprintHeld && input.Move.sqrMagnitude > 0.25f && CurrentSpeed >= runSpeed * 0.8f;
        public bool CanSlide => Time.time >= slideReadyAt && IsSprinting && body.isGrounded;
        public bool IsInvulnerable
        {
            get => health.IsInvulnerable;
            set => health.IsInvulnerable = value;
        }
        public string CurrentStateName => stateMachine.Current?.GetType().Name ?? "None";

        public PlayerLocomotionState LocomotionState { get; private set; }
        public PlayerDodgeState DodgeState { get; private set; }
        public PlayerJumpState JumpUpState { get; private set; }
        public PlayerJumpState RunJumpState { get; private set; }
        public PlayerSlideState SlideState { get; private set; }
        public PlayerDeathState DeathState { get; private set; }
        public PlayerAttackState AttackState { get; private set; }
        public PlayerHitState HitState { get; private set; }
        public PlayerClimbState ClimbState { get; private set; }

        public MoveSet MoveSet => moveSet;
        public bool HasCombat => moveSet != null;
        public bool IsInAir => !body.isGrounded;

        void Awake()
        {
            body = GetComponent<CharacterController>();
            if (cameraTransform == null) cameraTransform = Camera.main.transform;

            LocomotionState = new PlayerLocomotionState(this);
            DodgeState = new PlayerDodgeState(this);
            JumpUpState = new PlayerJumpState(this, running: false);
            RunJumpState = new PlayerJumpState(this, running: true);
            SlideState = new PlayerSlideState(this);
            DeathState = new PlayerDeathState(this);
            AttackState = new PlayerAttackState(this);
            HitState = new PlayerHitState(this);
            ClimbState = new PlayerClimbState(this);
        }

        void Start()
        {
            stateMachine.ChangeState(LocomotionState);
            health.Died += OnDied;
            health.Damaged += OnDamaged;
            if (swordTrail == null) swordTrail = GetComponentInChildren<SwordTrail>(true);
            shakeSource = GetComponent<CinemachineImpulseSource>();
            if (shakeSource == null && moveSet != null) shakeSource = gameObject.AddComponent<CinemachineImpulseSource>();
        }

        void OnDestroy()
        {
            if (health == null) return;
            health.Died -= OnDied;
            health.Damaged -= OnDamaged;
        }

        void Update()
        {
            // a short freeze when a hit lands: animation and the state both stop
            bool frozen = Time.unscaledTime < hitStopUntil;
            animator.speed = frozen ? 0f : 1f;
            stateMachine.Tick(frozen ? 0f : Time.deltaTime);
#if UNITY_EDITOR
            EditorTestKeys();
#endif
        }

        public void ChangeState(IState next) => stateMachine.ChangeState(next);

        // Health reached zero: knocked down.
        void OnDied(DamageInfo _) => ChangeState(DeathState);

        // Took a hit: flinch (or stagger from a big hit) unless busy with something that cannot be interrupted. Only characters with a move set
        // have the reaction clips for now.
        void OnDamaged(DamageInfo info)
        {
            if (health.IsDead || moveSet == null) return;
            IState current = stateMachine.Current;
            if (current == DeathState || current == HitState || current == ClimbState) return;
            if (current == AttackState && !AttackState.CanBeInterrupted) return;
            bool heavy = info.IsHeavy || info.Amount >= moveSet.staggerDamage;
            Vector3 from = info.Source != null ? info.Source.transform.position : transform.position - transform.forward;
            HitState.Begin(heavy, from);
            ChangeState(HitState);
        }

        // ---------------------------------------------------------------- climbing

        // Space next to a ledge: climb it instead of jumping. Looks the way the stick points first, then the way the character faces.
        public bool TryClimb()
        {
            if (!climb.enabled || Time.time < climbReadyAt || !body.isGrounded || health.IsDead) return false;

            Vector3 wish = CameraRelative(input.Move);
            if (wish.sqrMagnitude > 0.09f && LedgeProbe.TryFind(transform, body.radius, body.height, climb, wish, out LedgeProbe.Ledge toward))
                return StartClimb(toward);
            if (LedgeProbe.TryFind(transform, body.radius, body.height, climb, transform.forward, out LedgeProbe.Ledge ahead))
                return StartClimb(ahead);
            return false;
        }

        bool StartClimb(LedgeProbe.Ledge ledge)
        {
            ClimbState.Begin(ledge);
            ChangeState(ClimbState);
            return true;
        }

        public void ClimbEnded() => climbReadyAt = Time.time + climb.cooldown;

        // The climb moves the transform by itself, so the character controller must not push back meanwhile.
        public void SetBodyEnabled(bool on)
        {
            body.enabled = on;
            if (on) verticalVelocity = 0f;
        }

        // ---------------------------------------------------------------- combat helpers

        public void SetAnimSpeed(float speed) => animator.SetFloat(AnimSpeedParam, speed);

        // Freezes the character for a moment (real time), the "weight" of a landed hit.
        public void HitStop(float seconds)
        {
            if (seconds > 0f) hitStopUntil = Mathf.Max(hitStopUntil, Time.unscaledTime + seconds);
        }

        public void Shake(float force)
        {
            if (shakeSource != null && force > 0f) shakeSource.GenerateImpulseWithForce(force);
        }

        public void SetTrail(bool on) => swordTrail?.SetEmitting(on);

        // Starts an attack (or spell) if there is stamina for it.
        public bool TryAttack(AttackData data, int comboIndex)
        {
            if (data == null || health.IsDead) return false;
            if (data.staminaCost > 0f && !stamina.TryConsume(data.staminaCost)) return false;
            AttackState.Begin(data, comboIndex);
            ChangeState(AttackState);
            return true;
        }

        // Spell 0..2 (U / I / O): when it is off cooldown.
        public bool TryCast(int index)
        {
            if (moveSet == null || moveSet.spells == null || index < 0 || index >= moveSet.spells.Length) return false;
            SpellData spell = moveSet.spells[index];
            if (spell == null || !SpellReady(index) || !TryAttack(spell, -1)) return false;
            spellReadyAt[index] = Time.time + spell.cooldown;
            return true;
        }

        public bool SpellReady(int index) => index >= 0 && index < spellReadyAt.Length && Time.time >= spellReadyAt[index];

        public float SpellCooldownLeft(int index) => Mathf.Max(0f, spellReadyAt[index] - Time.time);

        // Turns to the way the stick points (camera relative) at once, so an attack goes where the player aims.
        public void FaceInput(float maxDegrees)
        {
            Vector3 direction = CameraRelative(input.Move);
            if (direction.sqrMagnitude > 0.04f) FaceTowards(direction, maxDegrees);
        }

        // Stops any rise or fall (starting an attack in the air).
        public void CancelVertical(float downwardSpeed = 0f) => verticalVelocity = -Mathf.Abs(downwardSpeed);

        // Gets a knocked-down character back on its feet with full health.
        public void Revive()
        {
            if (!health.IsDead) return;
            health.Revive();
            ChangeState(LocomotionState);
        }

#if UNITY_EDITOR
        // Test keys while there are no enemies yet: L = knocked down now, R = get up again.
        void EditorTestKeys()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.lKey.wasPressedThisFrame) health.TakeDamage(new DamageInfo(health.Max * 10f, gameObject));
            if (keyboard.rKey.wasPressedThisFrame) Revive();
        }
#endif

        // Jump: the vertical speed that reaches `height` metres under this controller's gravity. The arc is then plain physics in Move().
        public void Jump(float height) => verticalVelocity = Mathf.Sqrt(2f * -gravity * Mathf.Max(height, 0.01f));

        public void SetSpeed(float speed)
        {
            CurrentSpeed = Mathf.Max(0f, speed);
            animator.SetFloat(LocomotionBlendParam, LocomotionBlend(CurrentSpeed));
        }

        public void SlideEnded() => slideReadyAt = Time.time + slide.cooldown;

        public Vector3 CameraRelative(Vector2 moveInput)
        {
            Vector3 forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
            return Vector3.ClampMagnitude(forward * moveInput.y + right * moveInput.x, 1f);
        }

        public void Locomote(Vector2 moveInput, float deltaTime)
        {
            Vector3 direction = CameraRelative(moveInput);
            float targetSpeed = TopSpeed(moveInput) * direction.magnitude;
            float rate = targetSpeed > CurrentSpeed ? acceleration : deceleration;
            CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, targetSpeed, rate * deltaTime);

            if (direction.sqrMagnitude > 0.0001f)
                FaceTowards(direction, turnSpeed * deltaTime);

            // Moving along facing (not raw input) makes turns arc naturally instead of moonwalking.
            Move(transform.forward * (CurrentSpeed * deltaTime), deltaTime);
            animator.SetFloat(LocomotionBlendParam, LocomotionBlend(CurrentSpeed));
        }

        public void MatchSpeedToInput()
        {
            CurrentSpeed = TopSpeed(input.Move) * CameraRelative(input.Move).magnitude;
            animator.SetFloat(LocomotionBlendParam, LocomotionBlend(CurrentSpeed));
        }

        // Run is the normal top speed; holding Sprint while really moving (not a light stick tilt) raises it to the sprint clip's speed.
        float TopSpeed(Vector2 moveInput) => input.SprintHeld && moveInput.sqrMagnitude > 0.25f ? sprintSpeed : runSpeed;

        public float TopSpeedFor(Vector2 moveInput) => TopSpeed(moveInput) * CameraRelative(moveInput).magnitude;

        // Blend tree thresholds are Idle 0 / Walk 1 / Run 2 / Sprint 3, so one shared controller works for characters whose clips move at different speeds.
        float LocomotionBlend(float speed)
        {
            if (speed <= walkSpeed) return speed / walkSpeed;
            if (speed <= runSpeed) return 1f + (speed - walkSpeed) / (runSpeed - walkSpeed);
            return 2f + Mathf.Clamp01((speed - runSpeed) / Mathf.Max(sprintSpeed - runSpeed, 0.01f));
        }

        public void FaceTowards(Vector3 direction, float maxDegrees)
        {
            Quaternion target = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, maxDegrees);
        }

        public void Move(Vector3 horizontalDelta, float deltaTime)
        {
            if (body.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity += gravity * deltaTime;
            body.Move(horizontalDelta + Vector3.up * (verticalVelocity * deltaTime));
        }
    }
}
