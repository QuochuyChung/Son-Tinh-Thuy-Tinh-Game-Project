using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Interactions;

namespace SonTinhThuyTinh.Player
{
    public class PlayerInputReader : MonoBehaviour
    {
        const int SpellCount = 3;

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
        InputAction attack;        // J / gamepad West: a tap is a light attack, a hold is a heavy one
        InputAction lightAttack;   // left mouse button
        InputAction heavyAttack;   // right mouse button / gamepad North
        readonly InputAction[] spells = new InputAction[SpellCount];

        float dodgePressedAt = float.NegativeInfinity;
        float jumpPressedAt = float.NegativeInfinity;
        float slidePressedAt = float.NegativeInfinity;
        float lightPressedAt = float.NegativeInfinity;
        float heavyPressedAt = float.NegativeInfinity;
        readonly float[] spellPressedAt = { float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity };

        // AI override (BossAI): while active the stick and sprint come from the AI, and the device map is
        // switched off so a second set of hands on the same keyboard cannot fight it for control.
        bool aiActive;
        Vector2 aiMove;
        bool aiSprint;

        public bool AiActive => aiActive;
        public Vector2 Move => aiActive ? aiMove : move.ReadValue<Vector2>();
        // Held, not pressed: Shift (or left stick click) while moving makes the character sprint.
        public bool SprintHeld => aiActive ? aiSprint : sprint.IsPressed();
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
            attack = map.FindAction("Attack", throwIfNotFound: true);
            lightAttack = map.FindAction("LightAttack", throwIfNotFound: true);
            heavyAttack = map.FindAction("HeavyAttack", throwIfNotFound: true);
            for (int i = 0; i < SpellCount; i++) spells[i] = map.FindAction("Spell" + (i + 1), throwIfNotFound: true);
        }

        void OnEnable()
        {
            dodge.performed += OnDodge;
            jump.performed += OnJump;
            slide.performed += OnSlide;
            attack.performed += OnAttack;
            lightAttack.performed += OnLight;
            heavyAttack.performed += OnHeavy;
            for (int i = 0; i < SpellCount; i++) spells[i].performed += OnSpell;
            map.Enable();
            if (aiActive) map.Disable();   // a re-enabled reader must not let the keyboard fight the AI
        }

        void OnDisable()
        {
            dodge.performed -= OnDodge;
            jump.performed -= OnJump;
            slide.performed -= OnSlide;
            attack.performed -= OnAttack;
            lightAttack.performed -= OnLight;
            heavyAttack.performed -= OnHeavy;
            for (int i = 0; i < SpellCount; i++) spells[i].performed -= OnSpell;
            map.Disable();
        }

        void OnDestroy() => Destroy(runtimeActions);

        public bool ConsumeDodge() => Consume(ref dodgePressedAt);

        // Space / gamepad South. Becomes a running jump when the character is sprinting (decided by the locomotion state).
        public bool ConsumeJump() => Consume(ref jumpPressedAt);

        // C / gamepad right shoulder. Only slides while sprinting (decided by the locomotion state).
        public bool ConsumeSlide() => Consume(ref slidePressedAt);

        // A tap of J, or the left mouse button.
        public bool ConsumeLight() => Consume(ref lightPressedAt);

        // Holding J, or the right mouse button.
        public bool ConsumeHeavy() => Consume(ref heavyPressedAt);

        // U / I / O (index 0..2), gamepad D-pad left / up / right.
        public bool ConsumeSpell(int index) => Consume(ref spellPressedAt[index]);

        // Forgets presses that were made while the character could not act (so they do not fire the moment it lands).
        public void ClearBuffered()
        {
            dodgePressedAt = float.NegativeInfinity;
            jumpPressedAt = float.NegativeInfinity;
            slidePressedAt = float.NegativeInfinity;
            lightPressedAt = float.NegativeInfinity;
            heavyPressedAt = float.NegativeInfinity;
            for (int i = 0; i < SpellCount; i++) spellPressedAt[i] = float.NegativeInfinity;
        }

        // --- AI override: a boss drives this reader by setting the stick and stamping the same buffers the
        // keyboard uses, so every state machine rule (TryFight, TryCancel, stamina) is shared untouched. ---

        public void AiActivate()
        {
            aiActive = true;
            aiMove = Vector2.zero;
            aiSprint = false;
            ClearBuffered();
            if (map != null) map.Disable();   // no device can interfere while the AI is driving
        }

        public void AiDeactivate()
        {
            aiActive = false;
            aiMove = Vector2.zero;
            aiSprint = false;
            ClearBuffered();
            if (map != null && isActiveAndEnabled) map.Enable();
        }

        public void AiSetLocomotion(Vector2 moveInput, bool sprint)
        {
            aiMove = moveInput;
            aiSprint = sprint;
        }

        // Stamp the same timestamps OnLight/OnHeavy/OnSpell would, so the 0.2s buffer window still applies.
        public void AiPressLight() => lightPressedAt = Time.time;

        public void AiPressHeavy() => heavyPressedAt = Time.time;

        public void AiPressSpell(int index)
        {
            if (index >= 0 && index < SpellCount) spellPressedAt[index] = Time.time;
        }

        void OnDodge(InputAction.CallbackContext _) => dodgePressedAt = Time.time;

        void OnJump(InputAction.CallbackContext _) => jumpPressedAt = Time.time;

        void OnSlide(InputAction.CallbackContext _) => slidePressedAt = Time.time;

        void OnLight(InputAction.CallbackContext _) => lightPressedAt = Time.time;

        void OnHeavy(InputAction.CallbackContext _) => heavyPressedAt = Time.time;

        // The Attack action has a Tap and a Hold interaction: whichever one completed tells the kind of attack.
        void OnAttack(InputAction.CallbackContext context)
        {
            if (context.interaction is HoldInteraction) heavyPressedAt = Time.time;
            else lightPressedAt = Time.time;
        }

        void OnSpell(InputAction.CallbackContext context)
        {
            for (int i = 0; i < SpellCount; i++)
                if (context.action == spells[i]) spellPressedAt[i] = Time.time;
        }

        bool Consume(ref float pressedAt)
        {
            if (Time.time - pressedAt > bufferWindow) return false;
            pressedAt = float.NegativeInfinity;
            return true;
        }
    }
}
