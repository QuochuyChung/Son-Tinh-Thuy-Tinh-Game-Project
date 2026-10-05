using UnityEngine;
using UnityEngine.InputSystem;

namespace SonTinhThuyTinh.Player
{
    public class PlayerInputReader : MonoBehaviour
    {
        [SerializeField] InputActionAsset actions;
        [Tooltip("A button press is remembered this long, so pressing slightly early still triggers the next action.")]
        [SerializeField] float bufferWindow = 0.2f;

        InputActionAsset runtimeActions;
        InputActionMap map;
        InputAction move;
        InputAction look;
        InputAction dodge;
        InputAction sprint;
        InputAction jump;
        InputAction slide;
        float dodgePressedAt = float.NegativeInfinity;
        float jumpPressedAt = float.NegativeInfinity;
        float slidePressedAt = float.NegativeInfinity;

        public Vector2 Move => move.ReadValue<Vector2>();
        // Held, not pressed: Shift (or left stick click) while moving makes the character sprint.
        public bool SprintHeld => sprint.IsPressed();
        public Vector2 Look => look.ReadValue<Vector2>();
        public bool LookFromMouse => look.activeControl?.device is Pointer;

        void Awake()
        {
            // Private copy: disabling one reader (e.g. destroying a player) must not disable another reader's input.
            runtimeActions = Instantiate(actions);
            map = runtimeActions.FindActionMap("Player", throwIfNotFound: true);
            move = map.FindAction("Move", throwIfNotFound: true);
            look = map.FindAction("Look", throwIfNotFound: true);
            dodge = map.FindAction("Dodge", throwIfNotFound: true);
            sprint = map.FindAction("Sprint", throwIfNotFound: true);
            jump = map.FindAction("Jump", throwIfNotFound: true);
            slide = map.FindAction("Slide", throwIfNotFound: true);
        }

        void OnEnable()
        {
            dodge.performed += OnDodge;
            jump.performed += OnJump;
            slide.performed += OnSlide;
            map.Enable();
        }

        void OnDisable()
        {
            dodge.performed -= OnDodge;
            jump.performed -= OnJump;
            slide.performed -= OnSlide;
            map.Disable();
        }

        void OnDestroy() => Destroy(runtimeActions);

        public bool ConsumeDodge() => Consume(ref dodgePressedAt);

        // Space / gamepad South. Becomes a running jump when the character is sprinting (decided by the locomotion state).
        public bool ConsumeJump() => Consume(ref jumpPressedAt);

        // C / gamepad right shoulder. Only slides while sprinting (decided by the locomotion state).
        public bool ConsumeSlide() => Consume(ref slidePressedAt);

        // Forgets presses that were made while the character could not act (so they do not fire the moment it lands).
        public void ClearBuffered()
        {
            dodgePressedAt = float.NegativeInfinity;
            jumpPressedAt = float.NegativeInfinity;
            slidePressedAt = float.NegativeInfinity;
        }

        void OnDodge(InputAction.CallbackContext _) => dodgePressedAt = Time.time;

        void OnJump(InputAction.CallbackContext _) => jumpPressedAt = Time.time;

        void OnSlide(InputAction.CallbackContext _) => slidePressedAt = Time.time;

        bool Consume(ref float pressedAt)
        {
            if (Time.time - pressedAt > bufferWindow) return false;
            pressedAt = float.NegativeInfinity;
            return true;
        }
    }
}
