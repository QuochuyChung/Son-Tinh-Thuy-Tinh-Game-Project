using SonTinhThuyTinh.Characters;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SonTinhThuyTinh.UI.CharacterSelect
{
    // One half of the select screen: big stacked name, P1 / CPU tag, subtitle, description, three skill rows and the roster card.
    // Layout is built by Assets/Editor/CharacterSelectBuilder.cs; this only fills in the texts and animates the highlight.
    public class CharacterSelectPanel : MonoBehaviour
    {
        [SerializeField] CanvasGroup content;
        [Tooltip("Black layer over this half's 3D figure while it is not highlighted.")]
        [SerializeField] Image dimmer;
        [SerializeField] TMP_Text tag;
        [SerializeField] TMP_Text nameTop;
        [SerializeField] TMP_Text nameBottom;
        [SerializeField] TMP_Text subtitle;
        [SerializeField] TMP_Text description;
        [SerializeField] TMP_Text[] moves;
        [SerializeField] Image rosterPortrait;
        [SerializeField] Image rosterFrame;

        [Header("Highlight")]
        [SerializeField] string playerTag = "P1";
        [SerializeField] string rivalTag = "CPU";
        [SerializeField] Color playerColor = new(0.25f, 0.78f, 1f);
        [SerializeField] Color rivalColor = new(0.40f, 0.86f, 0.38f);
        [SerializeField] float dimmedFigureAlpha = 0.5f;
        [SerializeField] float dimmedContentAlpha = 0.6f;
        [SerializeField] float blendSpeed = 8f;

        [Header("Layout")]
        [Tooltip("Width the layout under Content was built for. Content shrinks to fit when this half of the screen is narrower (windows narrower than 16:9).")]
        [SerializeField] float designWidth = 960f;

        float amount;       // 0 = dimmed, 1 = highlighted
        bool highlighted;

        void OnEnable() => FitContent();

        void OnRectTransformDimensionsChange() => FitContent();

        // The panel itself covers exactly one half of the screen (so the dimmer lines up with the split of the 3D scene);
        // the texts and rows are laid out in a fixed designWidth x 1080 frame that is scaled down when the half gets narrower.
        void FitContent()
        {
            if (content == null) return;
            float width = ((RectTransform)transform).rect.width;
            float scale = width > 0f ? Mathf.Min(1f, width / designWidth) : 1f;
            content.transform.localScale = new Vector3(scale, scale, 1f);
        }

        public void Setup(CharacterDefinition character)
        {
            // "Sơn Tinh" -> "Sơn" over "Tinh", the second word pushed right, like the stacked names of the reference.
            string[] words = character.DisplayName.Split(' ', 2);
            nameTop.text = words[0];
            nameBottom.text = words.Length > 1 ? words[1] : "";
            subtitle.text = $"({character.Epithet})";
            description.text = character.Description;

            for (int i = 0; i < moves.Length; i++)
                moves[i].text = i < character.SignatureMoves.Count ? character.SignatureMoves[i] : "";

            if (rosterPortrait != null)
            {
                rosterPortrait.sprite = character.Portrait;
                rosterPortrait.enabled = character.Portrait != null;
            }
        }

        public void SetHighlighted(bool value, bool instant = false)
        {
            highlighted = value;
            tag.text = value ? playerTag : rivalTag;
            if (instant) amount = value ? 1f : 0f;
            Apply();
        }

        void Update()
        {
            float target = highlighted ? 1f : 0f;
            if (Mathf.Approximately(amount, target)) return;

            amount = Mathf.MoveTowards(amount, target, blendSpeed * Time.unscaledDeltaTime);
            Apply();
        }

        void Apply()
        {
            float eased = Mathf.SmoothStep(0f, 1f, amount);
            var dim = dimmer.color; dim.a = Mathf.Lerp(dimmedFigureAlpha, 0f, eased); dimmer.color = dim;
            content.alpha = Mathf.Lerp(dimmedContentAlpha, 1f, eased);
            var frame = rosterFrame.color; frame.a = eased; rosterFrame.color = frame;
            tag.color = Color.Lerp(rivalColor, playerColor, highlighted ? 1f : 0f);
        }
    }
}
