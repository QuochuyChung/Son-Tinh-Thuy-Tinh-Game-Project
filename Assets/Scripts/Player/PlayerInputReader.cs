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
        float dodgePressedAt = float.NegativeInfinity;

        public Vector2 Move => move.ReadValue<Vector2>();
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
        }

        void OnEnable()
        {
            dodge.performed += OnDodge;
            map.Enable();
        }

        void OnDisable()
        {
            dodge.performed -= OnDodge;
            map.Disable();
        }

        void OnDestroy() => Destroy(runtimeActions);

        public bool ConsumeDodge() => Consume(ref dodgePressedAt);

        void OnDodge(InputAction.CallbackContext _) => dodgePressedAt = Time.time;

        bool Consume(ref float pressedAt)
        {
            if (Time.time - pressedAt > bufferWindow) return false;
            pressedAt = float.NegativeInfinity;
            return true;
        }
    }
}
