using System.Collections;
using SonTinhThuyTinh.Combat;
using SonTinhThuyTinh.Dialogue;
using SonTinhThuyTinh.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SonTinhThuyTinh.Quest
{
    // A boss fight inside Map_ThuyTinh (docs/ke-hoach-thuy-tinh-su-kien.md): Mập Chín Vây in Shark Bay and Rùa Chín Chân at the flooded
    // shrine, instead of the separate scenes Battle_Map9Vay / Battle_Rua9Chan. Same recipe as VoiChinNgaArenaEvent on Map_SonTinh:
    //  - The player steps into the arena: the intro plays once (the boss's lines), the boss's AI is switched on, a health bar shows.
    //  - Player knocked out: the boss calms down, the player comes back at the entrance with full health, the boss is healed and back
    //    on its mark; the fight starts again when the player walks in (no second intro).
    //  - Boss down: the gift is collected (banner), the objects in `openOnWin` (a net, a barrier) are switched off, the body goes away
    //    after a few seconds. Coming back to the map later (the gift is already ours) the arena is empty and the way is open.
    public class SeaBossArenaEvent : MonoBehaviour
    {
        [SerializeField] MonoBehaviour boss;                       // an IArenaBoss
        [SerializeField] GiftItem gift;
        [SerializeField] Transform arenaCentre;
        [SerializeField] Transform bossMark;
        [SerializeField] Transform playerMark;
        [SerializeField] DialogueRunner runner;
        [SerializeField] DialogueSequence intro;
        [SerializeField] string key = "boss";                      // for the "intro seen" memory (SeaQuest)
        [SerializeField] string title = "BOSS";
        [SerializeField] Color accent = new(0.55f, 0.92f, 1f);
        [Tooltip("The boss wakes up when the player comes this close to the arena's centre.")]
        [SerializeField] float triggerRadius = 20f;
        [Tooltip("The boss cannot leave this radius round the centre.")]
        [SerializeField] float arenaRadius = 16f;
        [SerializeField] float bossMaxHealth = 220f;
        [Tooltip("Switched off when the boss is beaten (a net across the way out, a barrier).")]
        [SerializeField] GameObject[] openOnWin;

        IArenaBoss ai;
        PlayerController player;
        Image bossFill;
        GameObject hud;
        bool fightOn, running, won, lockedWarned;

        void Start()
        {
            ai = boss as IArenaBoss;
            if (runner == null) runner = FindFirstObjectByType<DialogueRunner>(FindObjectsInactive.Include);
            if (ai == null) { Debug.LogError("SeaBossArenaEvent: the boss is not an IArenaBoss.", this); enabled = false; return; }
            if (GiftTracker.Has(gift)) { boss.gameObject.SetActive(false); won = true; Open(); return; }

            ai.Health.SetMaximum(bossMaxHealth);
            ai.Health.IsInvulnerable = true;
            ai.Health.Died += OnBossDied;
            boss.enabled = false;   // asleep until the player comes in
            boss.transform.SetPositionAndRotation(bossMark.position, bossMark.rotation);
        }

        void OnDestroy()
        {
            if (ai != null && ai.Health != null) ai.Health.Died -= OnBossDied;
        }

        void Update()
        {
            if (won || ai == null) return;
            FindPlayer();
            if (player == null || running) return;

            float d = Flat(player.transform.position - arenaCentre.position).magnitude;
            if (!QuestQuizProgress.IsUnlocked(gift))
            {
                if (d < triggerRadius && !lockedWarned)
                {
                    lockedWarned = true;
                    QuestQuizProgress.AnnounceLocked(gift);
                }
                else if (d > triggerRadius + 3f) lockedWarned = false;
                return;
            }
            if (!fightOn)
            {
                if (d < triggerRadius && !player.Health.IsDead) StartCoroutine(Begin());
            }
            else if (player.Health.IsDead) StartCoroutine(Retry());
            else if (d > triggerRadius + 30f) Sleep();   // walked far away: the boss goes back to its place
        }

        void LateUpdate()
        {
            // the boss swims / walks after the player: keep it inside the arena
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
            if (intro != null && !SeaQuest.IntroSeen(key))
            {
                SeaQuest.MarkIntroSeen(key);
                bool done = false;
                QuestDialogue.Play(runner, intro, () => done = true);
                while (!done) yield return null;
            }
            if (player == null || player.Health.IsDead) { running = false; yield break; }
            ShowHud(true);
            ai.ResetForRetry();
            ai.Health.IsInvulnerable = false;
            boss.enabled = true;
            fightOn = true;
            running = false;
        }

        IEnumerator Retry()
        {
            running = true;
            ai.SetBattleEnded();
            yield return new WaitForSeconds(3f);
            Sleep();
            Teleport(playerMark);
            player.Revive();
            yield return new WaitForSeconds(1f);
            running = false;   // the fight starts again when the player walks back into the arena (Update)
        }

        // the boss rests on its mark with full health, doing nothing
        void Sleep()
        {
            fightOn = false;
            boss.enabled = false;
            ai.SetBattleEnded();
            ai.Health.Revive();
            ai.Health.IsInvulnerable = true;
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
            GiftTracker.Collect(gift);   // the gift banner: "Bạn đã nhận được …"
            ShowHud(false);
            Open();
            yield return new WaitForSeconds(6f);
            if (boss != null) boss.gameObject.SetActive(false);
        }

        void Open()
        {
            if (openOnWin == null) return;
            foreach (var go in openOnWin) if (go != null) go.SetActive(false);
        }

        // ---------------------------------------------------------------- HUD

        void ShowHud(bool on)
        {
            if (on && hud == null) BuildHud();
            if (hud != null) hud.SetActive(on);
            if (on) OnBossHealth(ai.Health.Current, ai.Health.Max);
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
            hud = new GameObject(key + "BossHUD", typeof(Canvas), typeof(CanvasScaler));
            Canvas canvas = hud.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = hud.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform panel = Rect("BossPanel", hud.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(900f, 90f), new Vector2(0f, -34f));
            panel.gameObject.AddComponent<Image>().color = new Color(0.015f, 0.055f, 0.08f, 0.84f);
            TMP_FontAsset font = player != null && player.MoveSet != null ? player.MoveSet.hudFont : null;
            TMP_Text label = Label("BossName", panel, font, title, 30, accent);
            label.rectTransform.anchorMin = new Vector2(0f, 0.52f);
            label.rectTransform.anchorMax = new Vector2(1f, 1f);
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;

            RectTransform bar = Rect("HealthBar", panel, Vector2.zero, new Vector2(1f, 0.45f), Vector2.zero, Vector2.zero);
            bar.offsetMin = new Vector2(18f, 14f);
            bar.offsetMax = new Vector2(-18f, -2f);
            bar.gameObject.AddComponent<Image>().color = new Color(0.02f, 0.12f, 0.16f, 0.98f);
            RectTransform fill = Rect("Fill", bar, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            fill.offsetMin = new Vector2(4f, 4f);
            fill.offsetMax = new Vector2(-4f, -4f);
            bossFill = fill.gameObject.AddComponent<Image>();
            bossFill.color = Color.Lerp(accent, new Color(0.05f, 0.65f, 0.88f), 0.5f);

            ai.Health.Changed += OnBossHealth;
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
    }
}
