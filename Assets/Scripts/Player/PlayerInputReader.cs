using SonTinhThuyTinh.Combat.Skills;
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

        InputActionMap skillMap;
        InputAction skillE1;
        InputAction skillE2;
        InputAction skillUlt;
        float skillE1PressedAt = float.NegativeInfinity;
        float skillE2PressedAt = float.NegativeInfinity;
        float skillUltPressedAt = float.NegativeInfinity;

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
            skillMap = runtimeActions.FindActionMap("Skill", throwIfNotFound: true);
            skillE1 = skillMap.FindAction("E1", throwIfNotFound: true);
            skillE2 = skillMap.FindAction("E2", throwIfNotFound: true);
            skillUlt = skillMap.FindAction("Ult", throwIfNotFound: true);
        }

        void OnEnable()
        {
            dodge.performed += OnDodge;
            map.Enable();
            skillE1.performed += OnSkillE1;
            skillE2.performed += OnSkillE2;
            skillUlt.performed += OnSkillUlt;
            SkillGate.Changed += OnSkillGateChanged;
            if (SkillGate.IsOpen) skillMap.Enable();
        }

        void OnDisable()
        {
            dodge.performed -= OnDodge;
            map.Disable();
            skillE1.performed -= OnSkillE1;
            skillE2.performed -= OnSkillE2;
            skillUlt.performed -= OnSkillUlt;
            skillMap.Disable();
            SkillGate.Changed -= OnSkillGateChanged;
        }

        void OnDestroy() => Destroy(runtimeActions);

        public bool ConsumeDodge() => Consume(ref dodgePressedAt);

        public SkillSlot ConsumeSkill()
        {
            if (Consume(ref skillE1PressedAt)) return SkillSlot.E1;
            if (Consume(ref skillE2PressedAt)) return SkillSlot.E2;
            if (Consume(ref skillUltPressedAt)) return SkillSlot.Ult;
            return SkillSlot.None;
        }

        void OnDodge(InputAction.CallbackContext _) => dodgePressedAt = Time.time;
        void OnSkillE1(InputAction.CallbackContext _) => skillE1PressedAt = Time.time;
        void OnSkillE2(InputAction.CallbackContext _) => skillE2PressedAt = Time.time;
        void OnSkillUlt(InputAction.CallbackContext _) => skillUltPressedAt = Time.time;

        void OnSkillGateChanged(bool open)
        {
            if (open) skillMap.Enable();
            else skillMap.Disable();
        }

        bool Consume(ref float pressedAt)
        {
            if (Time.time - pressedAt > bufferWindow) return false;
            pressedAt = float.NegativeInfinity;
            return true;
        }
    }
}
