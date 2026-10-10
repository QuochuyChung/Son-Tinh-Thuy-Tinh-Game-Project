using System.Collections;
using SonTinhThuyTinh.Combat;
using SonTinhThuyTinh.Flow;
using SonTinhThuyTinh.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SonTinhThuyTinh.Quest
{
    // Owns the win/lose contract of the arena and its boss HUD.
    public class VoiChinNgaBattleController : MonoBehaviour
    {
        [SerializeField] VoiChinNgaBoss boss;
        [SerializeField] GiftItem elephantGift;
        [SerializeField] PlayerSpawner playerSpawner;
        [SerializeField, Min(0f)] float resultDelay = 2.2f;

        Image bossFill;
        TMP_Text resultLabel;
        PlayerController player;
        bool ended;

        public void Configure(VoiChinNgaBoss battleBoss, GiftItem gift, PlayerSpawner spawner)
        {
            boss = battleBoss;
            elephantGift = gift;
            playerSpawner = spawner;
        }

        IEnumerator Start()
        {
            // PlayerSpawner creates the selected character in Awake. One frame also lets
            // all Health components initialize before events and HUD values are read.
            yield return null;
            player = playerSpawner != null ? playerSpawner.Player : FindFirstObjectByType<PlayerController>();
            if (boss == null) boss = FindFirstObjectByType<VoiChinNgaBoss>();
            if (player == null || boss == null)
            {
                Debug.LogError("VoiChinNgaBattleController: arena is missing player or boss.");
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
            if (victory) GiftTracker.Collect(elephantGift);
            GameSession.CompleteEncounter(victory);
            if (resultLabel != null)
            {
                resultLabel.text = victory ? "CHIẾN THẮNG\nĐã thu phục Voi Chín Ngà" : "THẤT BẠI\nVoi Chín Ngà chưa được thu phục";
                resultLabel.color = victory ? new Color(1f, 0.84f, 0.25f) : new Color(1f, 0.32f, 0.24f);
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
            var canvasObject = new GameObject("BossHUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            RectTransform panel = Rect("BossPanel", canvasObject.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(900f, 90f), new Vector2(0f, -34f));
            Image panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = new Color(0.035f, 0.025f, 0.02f, 0.78f);

            TMP_FontAsset font = player.MoveSet != null ? player.MoveSet.hudFont : null;
            TMP_Text name = Label("BossName", panel, font, "VOI CHÍN NGÀ", 30, new Color(1f, 0.86f, 0.52f));
            name.rectTransform.anchorMin = new Vector2(0f, 0.52f);
            name.rectTransform.anchorMax = new Vector2(1f, 1f);
            name.rectTransform.offsetMin = name.rectTransform.offsetMax = Vector2.zero;

            RectTransform bar = Rect("HealthBar", panel, new Vector2(0f, 0f), new Vector2(1f, 0.45f), Vector2.zero, Vector2.zero);
            bar.offsetMin = new Vector2(18f, 14f);
            bar.offsetMax = new Vector2(-18f, -2f);
            Image background = bar.gameObject.AddComponent<Image>();
            background.color = new Color(0.12f, 0.08f, 0.06f, 0.95f);

            RectTransform fill = Rect("Fill", bar, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            fill.offsetMin = new Vector2(4f, 4f);
            fill.offsetMax = new Vector2(-4f, -4f);
            bossFill = fill.gameObject.AddComponent<Image>();
            bossFill.color = new Color(0.82f, 0.12f, 0.07f);

            resultLabel = Label("Result", canvasObject.transform, font, string.Empty, 48, Color.white);
            RectTransform resultRect = resultLabel.rectTransform;
            resultRect.anchorMin = new Vector2(0.2f, 0.38f);
            resultRect.anchorMax = new Vector2(0.8f, 0.62f);
            resultRect.offsetMin = resultRect.offsetMax = Vector2.zero;
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
