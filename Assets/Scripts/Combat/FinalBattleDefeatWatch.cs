using System.Collections;
using SonTinhThuyTinh.CameraSystem;
using SonTinhThuyTinh.Flow;
using SonTinhThuyTinh.Player;
using SonTinhThuyTinh.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SonTinhThuyTinh.Combat
{
    // Drop into Map_FinalBattle. Watches the player's Health: after death and a short pause it shows a
    // Game Over overlay with "Thử lại" (reload the arena) and "Về Menu" (back to MainMenu). Builds its
    // own UI so the scene needs nothing but this component on an empty GameObject.
    public class FinalBattleDefeatWatch : MonoBehaviour
    {
        [SerializeField] float defeatDelay = 2.5f;
        [SerializeField] string mainMenuScene = SceneNames.MainMenu;

        PlayerController player;
        CanvasGroup panel;
        bool shown;
        ThirdPersonCameraInput cameraInput;
        PauseMenu pauseMenu;

        void Start()
        {
            PlayerSpawner spawner = FindFirstObjectByType<PlayerSpawner>();
            player = spawner != null ? spawner.Player : null;
            if (player == null || player.Health == null)
            {
                Debug.LogWarning("FinalBattleDefeatWatch: no player Health found.");
                enabled = false;
                return;
            }
            player.Health.Died += OnPlayerDied;
        }

        void OnDestroy()
        {
            if (player != null && player.Health != null)
                player.Health.Died -= OnPlayerDied;
        }

        void OnPlayerDied(DamageInfo _)
        {
            if (shown) return;
            StartCoroutine(DefeatRoutine());
        }

        IEnumerator DefeatRoutine()
        {
            // Let the death animation play before the overlay covers it.
            yield return new WaitForSecondsRealtime(defeatDelay);
            GameSession.Outcome = DuelOutcome.BossWon;
            ShowGameOver();
        }

        // ---------------------------------------------------------------- overlay

        void ShowGameOver()
        {
            shown = true;

            // The overlay is mouse-driven: unlock the cursor so the buttons can be clicked.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Stop the camera look (it would keep rotating behind the overlay and re-lock the
            // cursor on the next click) and hide the pause menu — Esc must not open it on top
            // of Game Over. Both are scene objects, so a reload brings them back fresh.
            cameraInput = FindFirstObjectByType<ThirdPersonCameraInput>();
            if (cameraInput != null) cameraInput.enabled = false;
            pauseMenu = FindFirstObjectByType<PauseMenu>();
            if (pauseMenu != null) pauseMenu.enabled = false;

            var canvasGo = new GameObject("GameOverCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            if (FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            panel = canvasGo.AddComponent<CanvasGroup>();
            panel.alpha = 0f;

            // dim
            var dim = new GameObject("Dim", typeof(RectTransform), typeof(Image));
            dim.transform.SetParent(canvasGo.transform, false);
            Stretch((RectTransform)dim.transform);
            dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

            // title
            var title = CreateText(canvasGo.transform, "Title", "THUA LỘI", 72f, FontStyles.Bold, TextAlignmentOptions.Center);
            var titleRect = title.rectTransform;
            titleRect.anchorMin = titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.anchoredPosition = new Vector2(0f, 160f);
            titleRect.sizeDelta = new Vector2(800f, 90f);

            var subtitle = CreateText(canvasGo.transform, "Subtitle", "Bệ hạ... đã định đoạt.", 28f, FontStyles.Italic, TextAlignmentOptions.Center);
            var subRect = subtitle.rectTransform;
            subRect.anchorMin = subRect.anchorMax = new Vector2(0.5f, 0.5f);
            subRect.anchoredPosition = new Vector2(0f, 90f);
            subRect.sizeDelta = new Vector2(800f, 40f);

            var retry = CreateButton(canvasGo.transform, "RetryButton", "Thử lại", new Vector2(0f, -20f));
            retry.onClick.AddListener(() =>
            {
                GameSession.Outcome = DuelOutcome.None;
                SceneLoader.Load(SceneNames.FinalBattle);
            });

            var menu = CreateButton(canvasGo.transform, "MenuButton", "Về Menu", new Vector2(0f, -110f));
            menu.onClick.AddListener(() => SceneLoader.Load(mainMenuScene));

            EventSystem.current.SetSelectedGameObject(retry.gameObject);
            StartCoroutine(CanvasGroupFade.Run(panel, 0f, 1f, 0.5f));
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static TMP_Text CreateText(Transform parent, string name, string content, float size, FontStyles style, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = align;
            text.color = Color.white;
            return text;
        }

        static Button CreateButton(Transform parent, string name, string label, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(320f, 64f);
            go.GetComponent<Image>().color = new Color(0.18f, 0.22f, 0.35f, 0.95f);
            var button = go.GetComponent<Button>();

            var text = CreateText(go.transform, "Label", label, 30f, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(text.rectTransform);
            return button;
        }
    }
}
