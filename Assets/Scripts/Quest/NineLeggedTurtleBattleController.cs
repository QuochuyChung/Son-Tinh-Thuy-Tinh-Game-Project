using System.Collections;
using SonTinhThuyTinh.Combat;
using SonTinhThuyTinh.Flow;
using SonTinhThuyTinh.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SonTinhThuyTinh.Quest
{
    // Owns the turtle arena result, two-way health flow, reward and return to Map_ThuyTinh.
    public sealed class NineLeggedTurtleBattleController : MonoBehaviour
    {
        [SerializeField] NineLeggedTurtleBoss boss;
        [SerializeField] GiftItem turtleGift;
        [SerializeField] PlayerSpawner playerSpawner;
        [SerializeField, Min(0f)] float resultDelay = 3f;

        Image bossFill;
        TMP_Text resultLabel;
        PlayerController player;
        bool ended;

        public void Configure(NineLeggedTurtleBoss battleBoss, GiftItem gift, PlayerSpawner spawner)
        {
            boss = battleBoss;
            turtleGift = gift;
            playerSpawner = spawner;
        }

        IEnumerator Start()
        {
            yield return null;
            player = playerSpawner != null ? playerSpawner.Player : FindFirstObjectByType<PlayerController>();
            if (boss == null) boss = FindFirstObjectByType<NineLeggedTurtleBoss>();
            if (player == null || boss == null)
            {
                Debug.LogError("NineLeggedTurtleBattleController: arena is missing Thuy Tinh or Rua Chin Chan.");
                yield break;
            }

            BuildBossHud();
            boss.Health.Changed += OnBossHealthChanged;
            boss.Health.Died += OnBossDied;
            player.Health.Died += OnPlayerDied;
            OnBossHealthChanged(boss.Health.Current, boss.Health.Max);
        }

        void OnDestroy()
        {
            if (boss != null && boss.Health != null)
            {
                boss.Health.Changed -= OnBossHealthChanged;
                boss.Health.Died -= OnBossDied;
            }
            if (player != null && player.Health != null) player.Health.Died -= OnPlayerDied;
        }

        void OnBossHealthChanged(float current, float max)
        {
            if (bossFill == null) return;
            Vector2 anchors = bossFill.rectTransform.anchorMax;
            anchors.x = max > 0f ? current / max : 0f;
            bossFill.rectTransform.anchorMax = anchors;
        }

        void OnBossDied(DamageInfo _) => Finish(true);

        void OnPlayerDied(DamageInfo _)
        {
            boss?.SetBattleEnded();
            Finish(false);
        }

        void Finish(bool victory)
        {
            if (ended) return;
            ended = true;
            if (victory) GiftTracker.Collect(turtleGift);
            GameSession.CompleteEncounter(victory);
            if (resultLabel != null)
            {
                resultLabel.text = victory
                    ? "CHIẾN THẮNG\nĐã thu phục Rùa Chín Chân"
                    : "THẤT BẠI\nRùa Chín Chân chưa được thu phục";
                resultLabel.color = victory ? new Color(0.4f, 1f, 0.55f) : new Color(1f, 0.32f, 0.24f);
                resultLabel.gameObject.SetActive(true);
            }
            StartCoroutine(ReturnToMap());
        }

        IEnumerator ReturnToMap()
        {
            yield return new WaitForSecondsRealtime(resultDelay);
            SceneLoader.Load(GameSession.EncounterReturnScene);
        }

        void BuildBossHud()
        {
            var canvasObject = new GameObject("TurtleBossHUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            RectTransform panel = Rect("BossPanel", canvasObject.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(900f, 90f), new Vector2(0f, -34f));
            panel.gameObject.AddComponent<Image>().color = new Color(0.025f, 0.09f, 0.055f, 0.88f);

            TMP_FontAsset font = player.MoveSet != null ? player.MoveSet.hudFont : null;
            TMP_Text name = Label("BossName", panel, font, "RÙA CHÍN CHÂN", 30, new Color(0.55f, 1f, 0.68f));
            name.rectTransform.anchorMin = new Vector2(0f, 0.52f);
            name.rectTransform.anchorMax = new Vector2(1f, 1f);
            name.rectTransform.offsetMin = name.rectTransform.offsetMax = Vector2.zero;

            RectTransform bar = Rect("HealthBar", panel, new Vector2(0f, 0f), new Vector2(1f, 0.45f), Vector2.zero, Vector2.zero);
            bar.offsetMin = new Vector2(18f, 14f);
            bar.offsetMax = new Vector2(-18f, -2f);
            bar.gameObject.AddComponent<Image>().color = new Color(0.02f, 0.13f, 0.065f, 0.98f);

            RectTransform fill = Rect("Fill", bar, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            fill.offsetMin = new Vector2(4f, 4f);
            fill.offsetMax = new Vector2(-4f, -4f);
            bossFill = fill.gameObject.AddComponent<Image>();
            bossFill.color = new Color(0.16f, 0.78f, 0.34f);

            resultLabel = Label("Result", canvasObject.transform, font, string.Empty, 48, Color.white);
            resultLabel.rectTransform.anchorMin = new Vector2(0.2f, 0.38f);
            resultLabel.rectTransform.anchorMax = new Vector2(0.8f, 0.62f);
            resultLabel.rectTransform.offsetMin = resultLabel.rectTransform.offsetMax = Vector2.zero;
            resultLabel.gameObject.SetActive(false);
        }

        static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        static TMP_Text Label(string name, Transform parent, TMP_FontAsset font, string text, float size, Color color)
        {
            RectTransform rect = Rect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            TMP_Text label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = FontStyles.Bold;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }
    }
}
