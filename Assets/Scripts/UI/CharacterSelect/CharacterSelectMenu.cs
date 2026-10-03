using System;
using SonTinhThuyTinh.Characters;
using SonTinhThuyTinh.Flow;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SonTinhThuyTinh.UI.CharacterSelect
{
    // Split-screen character select: the highlighted side is the player (P1), the other one becomes the final opponent (CPU).
    // Left / right (arrows, A / D, stick) or the mouse move the highlight, Enter / click / A confirms, Esc / B goes back.
    public class CharacterSelectMenu : MonoBehaviour
    {
        [Serializable]
        class Option
        {
            public CharacterDefinition character;
            public Button button;
            public CharacterSelectPanel panel;
            public CharacterPreview preview;
        }

        [SerializeField] Option[] options;

        InputAction back;
        Option highlighted;
        bool confirmed;

        void Awake()
        {
            back = new InputAction("Back", InputActionType.Button);
            back.AddBinding("<Keyboard>/escape");
            back.AddBinding("<Gamepad>/buttonEast");
        }

        void OnEnable() => back.Enable();

        void OnDisable() => back.Disable();

        void OnDestroy() => back.Dispose();

        void Start()
        {
            foreach (Option option in options)
            {
                option.panel.Setup(option.character);
                option.preview.Show(option.character);
                option.button.onClick.AddListener(() => Confirm(option));
            }

            EventSystem.current.SetSelectedGameObject(options[0].button.gameObject);
            Highlight(options[0], instant: true);
        }

        void Update()
        {
            if (back.WasPressedThisFrame() && !confirmed && !SceneLoader.IsLoading)
            {
                confirmed = true;
                SceneLoader.Load(SceneNames.MainMenu);
                return;
            }

            GameObject selected = EventSystem.current.currentSelectedGameObject;

            // Clicking empty space clears the selection; restore it so keyboard/gamepad navigation keeps working.
            if (selected == null)
            {
                EventSystem.current.SetSelectedGameObject(highlighted.button.gameObject);
                return;
            }

            foreach (Option option in options)
                if (option != highlighted && option.button.gameObject == selected)
                    Highlight(option);
        }

        void Highlight(Option option, bool instant = false)
        {
            highlighted = option;
            foreach (Option other in options)
            {
                other.preview.SetHighlighted(other == option);
                other.panel.SetHighlighted(other == option, instant);
            }
        }

        void Confirm(Option option)
        {
            if (confirmed) return;
            confirmed = true;

            GameSession.SelectedCharacter = option.character.Id;
            SceneLoader.Load(option.character.GiftBranchScene);
        }
    }
}
