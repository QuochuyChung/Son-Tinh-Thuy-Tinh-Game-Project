using SonTinhThuyTinh.Combat;
using SonTinhThuyTinh.Core;
using SonTinhThuyTinh.Player.States;
using UnityEngine;

namespace SonTinhThuyTinh.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        static readonly int LocomotionBlendParam = Animator.StringToHash("LocomotionBlend");

        [SerializeField] PlayerInputReader input;
        [SerializeField] Animator animator;
        [SerializeField] Stamina stamina;
        [Tooltip("Movement is relative to this camera. Defaults to Camera.main.")]
        [SerializeField] Transform cameraTransform;

        [Header("Locomotion")]
        [Tooltip("Natural root speed (m/s) of this character's Walk clip at its current scale, so feet don't slide.")]
        [SerializeField] float walkSpeed = 2f;
        [Tooltip("Natural root speed (m/s) of this character's Run clip at its current scale. Also the top movement speed.")]
        [SerializeField] float runSpeed = 6.6f;
        [SerializeField] float acceleration = 12f;
        [SerializeField] float deceleration = 16f;
        [Tooltip("Degrees per second.")]
        [SerializeField] float turnSpeed = 720f;
        [SerializeField] float gravity = -20f;

        [Header("Dodge")]
        [SerializeField] DodgeSettings dodge = new();

        CharacterController body;
        readonly StateMachine stateMachine = new();
        float verticalVelocity;

        public PlayerInputReader InputReader => input;
        public Animator Animator => animator;
        public Stamina Stamina => stamina;
        public DodgeSettings Dodge => dodge;
        public float CurrentSpeed { get; private set; }
        public bool IsInvulnerable { get; set; }
        public string CurrentStateName => stateMachine.Current?.GetType().Name ?? "None";

        public PlayerLocomotionState LocomotionState { get; private set; }
        public PlayerDodgeState DodgeState { get; private set; }

        void Awake()
        {
            body = GetComponent<CharacterController>();
            if (cameraTransform == null) cameraTransform = Camera.main.transform;

            LocomotionState = new PlayerLocomotionState(this);
            DodgeState = new PlayerDodgeState(this);
        }

        void Start() => stateMachine.ChangeState(LocomotionState);

        void Update() => stateMachine.Tick(Time.deltaTime);

        public void ChangeState(IState next) => stateMachine.ChangeState(next);

        public Vector3 CameraRelative(Vector2 moveInput)
        {
            Vector3 forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
            return Vector3.ClampMagnitude(forward * moveInput.y + right * moveInput.x, 1f);
        }

        public void Locomote(Vector2 moveInput, float deltaTime)
        {
            Vector3 direction = CameraRelative(moveInput);
            float targetSpeed = runSpeed * direction.magnitude;
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
            CurrentSpeed = runSpeed * CameraRelative(input.Move).magnitude;
            animator.SetFloat(LocomotionBlendParam, LocomotionBlend(CurrentSpeed));
        }

        // Blend tree thresholds are Idle 0 / Walk 1 / Run 2, so one shared controller works for characters whose clips move at different speeds.
        float LocomotionBlend(float speed) =>
            speed <= walkSpeed ? speed / walkSpeed : 1f + (speed - walkSpeed) / (runSpeed - walkSpeed);

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
