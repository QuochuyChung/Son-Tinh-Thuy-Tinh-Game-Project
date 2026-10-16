using System.Collections.Generic;
using SonTinhThuyTinh.CameraSystem;
using SonTinhThuyTinh.Player;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SonTinhThuyTinh.Quest
{
    // Runtime modal used by the mission keeper. It is created on demand so both
    // branch maps share the exact same responsive layout and quiz behaviour.
    public sealed class QuestQuizUI : MonoBehaviour
    {
        static QuestQuizUI instance;

        readonly List<Button> optionButtons = new();
        readonly List<TMP_Text> optionLabels = new();
        List<QuestQuizQuestion> questions;
        GiftItem gift;
        TMP_Text progressLabel, categoryLabel, questionLabel, feedbackLabel, nextLabel;
        Button nextButton;
        int questionIndex, correct;
        float previousTimeScale;
        PlayerController player;
        ThirdPersonCameraInput cameraInput;
        bool playerInputWasEnabled, cameraWasEnabled, restored;

        public static bool IsOpen => instance != null;

        public static void Show(GiftItem target)
        {
            if (target == null || IsOpen) return;
            var go = new GameObject("QuestQuizUI");
            instance = go.AddComponent<QuestQuizUI>();
            instance.Open(target);
        }

        void Open(GiftItem target)
        {
            gift = target;
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;

            player = FindAnyObjectByType<PlayerController>();
            if (player != null && player.InputReader != null)
            {
                playerInputWasEnabled = player.InputReader.enabled;
                player.InputReader.ClearBuffered();
                player.InputReader.enabled = false;
            }
            cameraInput = FindAnyObjectByType<ThirdPersonCameraInput>();
            if (cameraInput != null)
            {
                cameraWasEnabled = cameraInput.enabled;
                cameraInput.enabled = false;
            }

            Build();
            BeginAttempt();
        }

        void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 120;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            RectTransform shade = Rect("Shade", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            shade.gameObject.AddComponent<Image>().color = new Color(0.01f, 0.015f, 0.025f, 0.82f);

            RectTransform panel = Rect("QuizPanel", shade, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(980f, 760f), Vector2.zero);
            panel.gameObject.AddComponent<Image>().color = new Color(0.035f, 0.055f, 0.075f, 0.98f);
            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.95f, 0.68f, 0.22f, 0.9f);
            outline.effectDistance = new Vector2(3f, -3f);

            Button close = MakeButton("Close", panel, new Vector2(900f, -22f), new Vector2(54f, 54f));
            TMP_Text closeLabel = close.GetComponentInChildren<TMP_Text>();
            closeLabel.text = "×";
            closeLabel.fontSize = 34f;
            closeLabel.alignment = TextAlignmentOptions.Center;
            closeLabel.margin = Vector4.zero;
            close.onClick.AddListener(Close);

            TMP_Text title = Label("Title", panel, "NGƯỜI GIỮ NHIỆM VỤ", 36, new Color(1f, 0.78f, 0.3f), FontStyles.Bold);
            Place(title.rectTransform, 48f, 34f, -48f, 74f);
            title.alignment = TextAlignmentOptions.Center;

            progressLabel = Label("Progress", panel, string.Empty, 24, Color.white, FontStyles.Bold);
            Place(progressLabel.rectTransform, 58f, 115f, -58f, 42f);
            categoryLabel = Label("Category", panel, string.Empty, 20, new Color(0.45f, 0.85f, 1f), FontStyles.Italic);
            Place(categoryLabel.rectTransform, 58f, 158f, -58f, 34f);

            questionLabel = Label("Question", panel, string.Empty, 29, Color.white, FontStyles.Normal);
            Place(questionLabel.rectTransform, 58f, 198f, -58f, 142f);
            questionLabel.alignment = TextAlignmentOptions.TopLeft;
            questionLabel.textWrappingMode = TextWrappingModes.Normal;

            for (int i = 0; i < 4; i++)
            {
                int answer = i;
                Button button = MakeButton("Option" + i, panel, new Vector2(58f, -360f - i * 72f), new Vector2(864f, 58f));
                button.onClick.AddListener(() => Choose(answer));
                optionButtons.Add(button);
                optionLabels.Add(button.GetComponentInChildren<TMP_Text>());
            }

            feedbackLabel = Label("Feedback", panel, string.Empty, 20, new Color(0.8f, 0.9f, 1f), FontStyles.Normal);
            Place(feedbackLabel.rectTransform, 58f, 650f, -310f, 72f);
            feedbackLabel.alignment = TextAlignmentOptions.TopLeft;
            feedbackLabel.textWrappingMode = TextWrappingModes.Normal;

            nextButton = MakeButton("Next", panel, new Vector2(710f, -665f), new Vector2(212f, 62f));
            nextLabel = nextButton.GetComponentInChildren<TMP_Text>();
            nextButton.onClick.AddListener(Next);
        }

        void Update()
        {
            bool escape = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            bool back = Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame;
            if (escape || back) Close();
        }

        void BeginAttempt()
        {
            nextButton.onClick.RemoveAllListeners();
            nextButton.onClick.AddListener(Next);
            questionLabel.alignment = TextAlignmentOptions.TopLeft;
            questions = QuestQuizDatabase.PickThree();
            questionIndex = 0;
            correct = 0;
            if (questions.Count < 3)
            {
                feedbackLabel.text = "Không đủ dữ liệu câu hỏi. Hãy kiểm tra Resources/Questions/question.json.";
                nextLabel.text = "ĐÓNG";
                nextButton.gameObject.SetActive(true);
                nextButton.onClick.RemoveAllListeners();
                nextButton.onClick.AddListener(Close);
                return;
            }
            ShowQuestion();
        }

        void ShowQuestion()
        {
            QuestQuizQuestion q = questions[questionIndex];
            progressLabel.text = $"Câu {questionIndex + 1}/3   •   Đúng: {correct}/3   •   Mở khóa: {gift.DisplayName}";
            categoryLabel.text = $"{q.category}  ·  {q.difficulty}";
            questionLabel.text = q.question;
            feedbackLabel.text = "Chọn một đáp án. Cần đúng ít nhất 2/3 câu.";
            feedbackLabel.color = new Color(0.75f, 0.84f, 0.9f);
            nextButton.gameObject.SetActive(false);

            for (int i = 0; i < optionButtons.Count; i++)
            {
                bool exists = q.options != null && i < q.options.Length;
                optionButtons[i].gameObject.SetActive(exists);
                optionButtons[i].interactable = exists;
                optionButtons[i].image.color = new Color(0.10f, 0.16f, 0.21f, 1f);
                optionLabels[i].text = exists ? $"{(char)('A' + i)}.  {q.options[i]}" : string.Empty;
            }
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(optionButtons[0].gameObject);
        }

        void Choose(int answer)
        {
            QuestQuizQuestion q = questions[questionIndex];
            foreach (Button button in optionButtons) button.interactable = false;
            bool right = answer == q.correct_index;
            if (right) correct++;
            optionButtons[answer].image.color = right ? new Color(0.12f, 0.55f, 0.28f) : new Color(0.65f, 0.16f, 0.12f);
            if (!right && q.correct_index >= 0 && q.correct_index < optionButtons.Count)
                optionButtons[q.correct_index].image.color = new Color(0.12f, 0.55f, 0.28f);

            feedbackLabel.color = right ? new Color(0.42f, 1f, 0.58f) : new Color(1f, 0.48f, 0.38f);
            feedbackLabel.text = (right ? "Chính xác! " : "Chưa đúng. ") + q.explanation;
            nextLabel.text = questionIndex < 2 ? "CÂU TIẾP" : "XEM KẾT QUẢ";
            nextButton.gameObject.SetActive(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(nextButton.gameObject);
        }

        void Next()
        {
            if (questionIndex < 2)
            {
                questionIndex++;
                ShowQuestion();
                return;
            }
            ShowResult();
        }

        void ShowResult()
        {
            foreach (Button button in optionButtons) button.gameObject.SetActive(false);
            categoryLabel.text = "KẾT QUẢ THỬ THÁCH";
            bool passed = correct >= 2;
            questionLabel.text = passed
                ? $"Bạn trả lời đúng {correct}/3 câu.\n\nNhiệm vụ tìm <b>{gift.DisplayName}</b> đã được mở khóa!"
                : $"Bạn chỉ trả lời đúng {correct}/3 câu.\n\nCần đúng ít nhất 2/3 để nhận nhiệm vụ.";
            questionLabel.alignment = TextAlignmentOptions.Center;
            feedbackLabel.text = passed
                ? "Bảng nhiệm vụ và đường gạch trên bản đồ đã được cập nhật."
                : "Quái vật vẫn bị phong ấn và không thể chiến đấu. Bạn có thể thử lại với 3 câu ngẫu nhiên khác.";
            feedbackLabel.color = passed ? new Color(0.42f, 1f, 0.58f) : new Color(1f, 0.62f, 0.35f);

            nextButton.onClick.RemoveAllListeners();
            if (passed)
            {
                QuestQuizProgress.Unlock(gift);
                GiftTracker.Announce($"Đã nhận nhiệm vụ: tìm <b>{gift.DisplayName}</b>\nĐường chỉ dẫn đã xuất hiện trên bản đồ");
                nextLabel.text = "NHẬN NHIỆM VỤ";
                nextButton.onClick.AddListener(Close);
            }
            else
            {
                nextLabel.text = "THỬ LẠI";
                nextButton.onClick.AddListener(BeginAttempt);
            }
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(nextButton.gameObject);
        }

        void Close()
        {
            RestoreGameplay();
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            RestoreGameplay();
            if (instance == this) instance = null;
        }

        void RestoreGameplay()
        {
            if (restored) return;
            restored = true;
            Time.timeScale = previousTimeScale;
            if (player != null && player.InputReader != null) player.InputReader.enabled = playerInputWasEnabled;
            if (cameraInput != null) cameraInput.enabled = cameraWasEnabled;
        }

        static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 size, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        static void Place(RectTransform rect, float left, float top, float right, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = new Vector2(right, -top);
        }

        static TMP_Text Label(string name, Transform parent, string value, float size, Color color, FontStyles style)
        {
            RectTransform rect = Rect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset;
            label.text = value;
            label.fontSize = size;
            label.color = color;
            label.fontStyle = style;
            label.raycastTarget = false;
            return label;
        }

        static Button MakeButton(string name, Transform parent, Vector2 position, Vector2 size)
        {
            RectTransform rect = Rect(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), size, position);
            rect.pivot = new Vector2(0f, 1f);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.10f, 0.16f, 0.21f, 1f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.18f, 0.30f, 0.38f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.95f, 0.62f, 0.18f, 1f);
            button.colors = colors;
            TMP_Text label = Label("Label", rect, string.Empty, 22, Color.white, FontStyles.Normal);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.margin = new Vector4(22f, 4f, 18f, 4f);
            label.textWrappingMode = TextWrappingModes.Normal;
            return button;
        }
    }
}
