using System.Collections;
using SonTinhThuyTinh.Combat;
using SonTinhThuyTinh.Dialogue;
using SonTinhThuyTinh.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SonTinhThuyTinh.Quest
{
    // The fight with Voi Chín Ngà inside Map_SonTinh (built by Assets/Editor/ElephantArenaBuilder.cs, docs/progress.md 9.24), instead of
    // the separate scene Battle_VoiChinNga: the elephant waits in the arena at the foot of the last stop.
    //  - The player steps into the arena: the elephant speaks (Huy's intro lines), its AI (VoiChinNgaBoss) is switched on and a health bar
    //    shows at the top of the screen.
    //  - Player knocked out: the elephant calms down, the player comes back at the arena's entrance with full health, the elephant is
    //    healed and back on its mark; the fight starts again when the player walks in (no second intro).
    //  - Elephant down: Gift_VoiChinNga is collected (banner), the body lies there a few seconds and goes away. Coming back to the map
    //    later, the arena is empty (the gift is already collected).
    public class VoiChinNgaArenaEvent : MonoBehaviour
    {
        [SerializeField] VoiChinNgaBoss boss;
        [SerializeField] GiftItem gift;
        [SerializeField] Transform arenaCentre;
        [SerializeField] Transform bossMark;
        [SerializeField] Transform playerMark;
        [SerializeField] DialogueRunner runner;
        [Tooltip("The elephant wakes up when the player comes this close to the arena's centre.")]
        [SerializeField] float triggerRadius = 12f;   // inside the entrance (the player comes back at 14 m from the centre): a moment to breathe after a defeat
        [Tooltip("The elephant cannot leave this radius round the centre; also where its charge stops.")]
        [SerializeField] float arenaRadius = 17f;
        [SerializeField] float bossMaxHealth = 450f;

        PlayerController player;
        Image bossFill;
        GameObject hud;
        bool fightOn, running, won;
        DialogueSequence intro;

        void Start()
        {
            if (runner == null) runner = FindFirstObjectByType<DialogueRunner>(FindObjectsInactive.Include);
            if (GiftTracker.Has(gift)) { boss.gameObject.SetActive(false); won = true; return; }

            boss.SetArena(arenaCentre.position, arenaRadius);
            boss.Health.SetMaximum(bossMaxHealth);
            boss.Health.Died += OnBossDied;
            boss.enabled = false;   // asleep until the player comes in
            boss.transform.SetPositionAndRotation(bossMark.position, bossMark.rotation);
        }

        void OnDestroy()
        {
            if (boss != null && boss.Health != null) boss.Health.Died -= OnBossDied;
        }

        void Update()
        {
            if (won || boss == null) return;
            FindPlayer();
            if (player == null || running) return;

            float d = Flat(player.transform.position - arenaCentre.position).magnitude;
            if (!fightOn)
            {
                if (d < triggerRadius && !player.Health.IsDead) StartCoroutine(Begin());
            }
            else if (player.Health.IsDead) StartCoroutine(Retry());
            else if (d > triggerRadius + 25f) { Sleep(); }   // walked far away: the elephant goes back to its place
        }

        void LateUpdate()
        {
            // the elephant walks after the player: keep it on the floor of the arena
            if (!fightOn || won || boss == null) return;
            Vector3 off = Flat(boss.transform.position - arenaCentre.position);
            if (off.magnitude > arenaRadius)
            {
                Vector3 p = arenaCentre.position + off.normalized * arenaRadius;
                boss.transform.position = new Vector3(p.x, boss.transform.position.y, p.z);
            }
        }

        IEnumerator Begin()
        {
            running = true;
            if (!VoiChinNgaQuest.IntroSeen)
            {
                VoiChinNgaQuest.MarkIntroSeen();
                bool done = false;
                QuestDialogue.Play(runner, intro != null ? intro : (intro = BuildIntro()), () => done = true);
                while (!done) yield return null;
            }
            if (player == null || player.Health.IsDead) { running = false; yield break; }
            ShowHud(true);
            boss.ResetForRetry();
            boss.enabled = true;
            fightOn = true;
            running = false;
        }

        IEnumerator Retry()
        {
            running = true;
            boss.SetBattleEnded();
            yield return new WaitForSeconds(3f);
            Sleep();
            Teleport(playerMark);
            player.Revive();
            yield return new WaitForSeconds(1f);
            running = false;   // the fight starts again when the player walks back into the arena (Update)
        }

        // the elephant stands on its mark, full health, doing nothing
        void Sleep()
        {
            fightOn = false;
            boss.enabled = false;
            boss.SetBattleEnded();
            boss.Health.Revive();
            boss.transform.SetPositionAndRotation(bossMark.position, bossMark.rotation);
            ShowHud(false);
        }

        void OnBossDied(DamageInfo _)
        {
            if (won) return;
            won = true; fightOn = false;
            StopAllCoroutines();
            running = false;
            StartCoroutine(Victory());
        }

        IEnumerator Victory()
        {
            yield return new WaitForSeconds(1.5f);
            GiftTracker.Collect(gift);   // the gift banner: "Bạn đã nhận được Voi chín ngà"
            ShowHud(false);
            yield return new WaitForSeconds(6f);
            if (boss != null) boss.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------- HUD

        void ShowHud(bool on)
        {
            if (on && hud == null) BuildHud();
            if (hud != null) hud.SetActive(on);
            if (on) OnBossHealth(boss.Health.Current, boss.Health.Max);
        }

        void OnBossHealth(float current, float max)
        {
            if (bossFill == null) return;
            Vector2 a = bossFill.rectTransform.anchorMax;
            a.x = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            bossFill.rectTransform.anchorMax = a;
        }

        void BuildHud()
        {
            hud = new GameObject("ElephantBossHUD", typeof(Canvas), typeof(CanvasScaler));
            Canvas canvas = hud.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = hud.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform panel = Rect("BossPanel", hud.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(900f, 90f), new Vector2(0f, -34f));
            panel.gameObject.AddComponent<Image>().color = new Color(0.035f, 0.025f, 0.02f, 0.78f);
            TMP_FontAsset font = player != null && player.MoveSet != null ? player.MoveSet.hudFont : null;
            TMP_Text title = Label("BossName", panel, font, "VOI CHÍN NGÀ", 30, new Color(1f, 0.86f, 0.52f));
            title.rectTransform.anchorMin = new Vector2(0f, 0.52f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.offsetMin = title.rectTransform.offsetMax = Vector2.zero;

            RectTransform bar = Rect("HealthBar", panel, Vector2.zero, new Vector2(1f, 0.45f), Vector2.zero, Vector2.zero);
            bar.offsetMin = new Vector2(18f, 14f);
            bar.offsetMax = new Vector2(-18f, -2f);
            bar.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.08f, 0.06f, 0.95f);
            RectTransform fill = Rect("Fill", bar, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            fill.offsetMin = new Vector2(4f, 4f);
            fill.offsetMax = new Vector2(-4f, -4f);
            bossFill = fill.gameObject.AddComponent<Image>();
            bossFill.color = new Color(0.82f, 0.12f, 0.07f);

            boss.Health.Changed += OnBossHealth;
            hud.transform.SetParent(transform, false);
        }

        static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
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
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = FontStyles.Bold;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            return label;
        }

        // ---------------------------------------------------------------- player

        void FindPlayer()
        {
            if (player != null && player.isActiveAndEnabled) return;
            player = null;
            foreach (PlayerController c in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
                if (c.isActiveAndEnabled) { player = c; break; }
        }

        void Teleport(Transform mark)
        {
            if (player == null || mark == null) return;
            player.SetBodyEnabled(false);
            player.transform.SetPositionAndRotation(mark.position, mark.rotation);
            player.SetBodyEnabled(true);
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        static DialogueSequence BuildIntro() => DialogueSequence.CreateRuntime(
            new DialogueLine { speaker = "Dẫn chuyện", text = "Giữa thung lũng đá, Voi Chín Ngà đứng chắn lối. Mỗi nhịp thở của linh tượng làm mặt đất khẽ rung chuyển." },
            new DialogueLine { speaker = "Voi Chín Ngà", text = "Kẻ phàm nào dám bước vào lãnh địa của ta?" },
            new DialogueLine { speaker = "Sơn Tinh", text = "Ta là Sơn Tinh. Ta đến tìm Voi Chín Ngà làm sính lễ dâng Vua Hùng." },
            new DialogueLine { speaker = "Voi Chín Ngà", text = "Sính lễ không dành cho kẻ chỉ biết khoe sức. Hãy chứng minh ngươi đủ bản lĩnh bảo vệ Mị Nương." },
            new DialogueLine { speaker = "Dẫn chuyện", text = "Chín chiếc ngà rực sáng. Linh tượng hạ thấp thân mình, sẵn sàng tung ra những đòn rung núi chuyển rừng." },
            new DialogueLine { speaker = "Sơn Tinh", text = "Ta chấp nhận thử thách. Xin hãy xuất chiêu!" });
    }
}
