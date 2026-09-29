using System;
using SonTinhThuyTinh.Characters;
using SonTinhThuyTinh.Flow;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SonTinhThuyTinh.UI.CharacterSelect
{
    public class CharacterSelectMenu : MonoBehaviour
    {
        [Serializable]
        class Option
        {
            public CharacterDefinition character;
            public Button button;
            public TMP_Text label;
            public CharacterPreview preview;
        }

        [SerializeField] Option[] options;
        [SerializeField] TMP_Text descriptionText;

        Option highlighted;
        bool confirmed;

        void Start()
        {
            foreach (Option option in options)
            {
                option.label.text = $"{option.character.DisplayName}\n<size=55%>{option.character.Epithet}</size>";
                option.preview.Show(option.character);
                option.button.onClick.AddListener(() => Confirm(option));
            }

            EventSystem.current.SetSelectedGameObject(options[0].button.gameObject);
            Highlight(options[0]);
        }

        void Update()
        {
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

        void Highlight(Option option)
        {
            highlighted = option;
            foreach (Option other in options)
                other.preview.SetHighlighted(other == option);
            descriptionText.text = option.character.Description;
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
