using SonTinhThuyTinh.Combat.Boss;
using SonTinhThuyTinh.Combat.Environment;
using SonTinhThuyTinh.Combat.Skills;
using SonTinhThuyTinh.Player;
using UnityEngine;

namespace SonTinhThuyTinh.UI
{
    public sealed class BattleHud : MonoBehaviour
    {
        static readonly Color WaterColor = new Color(0.13f, 0.42f, 0.66f, 0.92f);
        static readonly Color EarthColor = new Color(0.50f, 0.37f, 0.17f, 0.92f);
        static readonly Color HpFill = new Color(0.78f, 0.14f, 0.12f, 1f);
        static readonly Color HpBg = new Color(0.07f, 0.07f, 0.08f, 0.88f);
        static readonly Color LitBorder = new Color(1f, 0.86f, 0.35f, 1f);
        static readonly Color DimBorder = new Color(0.45f, 0.45f, 0.48f, 0.7f);
        static readonly Color MarkerColor = new Color(1f, 1f, 1f, 0.95f);
        static readonly Color TimerOk = new Color(1f, 0.85f, 0.35f, 1f);
        static readonly Color TimerWarn = new Color(1f, 0.25f, 0.2f, 1f);
        static readonly Color IconBg = new Color(0.08f, 0.08f, 0.10f, 0.88f);

        BossHealth boss;
        EnvironmentDirector env;
        BattleFlow flow;
        PlayerController player;

        float bossHpFraction = 1f;
        int bossSegment = 1;
        int bossSegments = 3;
        bool bossExhausted;
        string bossName = "Boss";
        int envLevel;
        bool reclaimActive;

        GUIStyle titleStyle;
        GUIStyle timerStyle;
        GUIStyle keyStyle;
        GUIStyle cdStyle;
        GUIStyle axisStyle;

        void Start()
        {
            boss = BossHealth.Instance;
            env = EnvironmentDirector.Instance;
            flow = BattleFlow.Instance;
            player = FindFirstObjectByType<PlayerController>();

            if (boss != null)
            {
                bossName = boss.name;
                bossHpFraction = boss.HpFraction;
                bossSegment = boss.CurrentSegment;
                bossSegments = boss.Segments;
                bossExhausted = boss.IsExhausted;
                boss.HpChanged += OnBossHp;
                boss.PhaseStarted += OnPhaseStarted;
                boss.Exhausted += OnBossExhausted;
            }
            if (env != null)
            {
                envLevel = env.Level;
                env.LevelChanged += OnEnvLevel;
            }
            if (flow != null)
            {
                reclaimActive = flow.ReclaimActive;
                flow.ReclaimStarted += OnReclaimStarted;
                flow.ReclaimStopped += OnReclaimStopped;
                flow.BattleEnded += OnBattleEnded;
            }
        }

        void OnDestroy()
        {
            if (boss != null)
            {
                boss.HpChanged -= OnBossHp;
                boss.PhaseStarted -= OnPhaseStarted;
                boss.Exhausted -= OnBossExhausted;
            }
            if (env != null) env.LevelChanged -= OnEnvLevel;
            if (flow != null)
            {
                flow.ReclaimStarted -= OnReclaimStarted;
                flow.ReclaimStopped -= OnReclaimStopped;
                flow.BattleEnded -= OnBattleEnded;
            }
        }

        void OnBossHp(float hp, float max) => bossHpFraction = max > 0f ? Mathf.Clamp01(hp / max) : 0f;
        void OnPhaseStarted(int segment) => bossSegment = segment;
        void OnBossExhausted() => bossExhausted = true;
        void OnEnvLevel(int level, int delta) => envLevel = level;
        void OnReclaimStarted(int phase) => reclaimActive = true;
        void OnReclaimStopped() => reclaimActive = false;
        void OnBattleEnded(BattleState state, string reason) => reclaimActive = false;

        void OnGUI()
        {
            EnsureStyles();
            DrawBossBar();
            DrawEnvAxis();
            DrawReclaimTimer();
            if (SkillGate.IsOpen) DrawCooldownIcons();
        }

        void DrawBossBar()
        {
            if (boss == null) return;

            float w = Mathf.Min(760f, Screen.width - 40f);
            float x = (Screen.width - w) * 0.5f;
            string phase = bossExhausted ? "CẠN KIỆT" : $"Đoạn {bossSegment}/{bossSegments}";
            GUI.Label(new Rect(x, 16f, w, 22f), $"{bossName} — {phase}", titleStyle);

            const float gap = 6f;
            const float h = 18f;
            float segW = (w - gap * (bossSegments - 1)) / bossSegments;
            float y = 40f;
            for (int i = 1; i <= bossSegments; i++)
            {
                Rect seg = new Rect(x + (i - 1) * (segW + gap), y, segW, h);
                DrawRect(seg, HpBg);

                bool isCurrent = i == bossSegment && !bossExhausted;
                if (isCurrent)
                {
                    DrawRect(new Rect(seg.x + 2f, seg.y + 2f, (seg.width - 4f) * bossHpFraction, seg.height - 4f), HpFill);
                }
                DrawRectBorder(seg, isCurrent ? LitBorder : DimBorder, isCurrent ? 2f : 1f);
            }
        }

        void DrawEnvAxis()
        {
            if (env == null) return;

            const float w = 460f;
            const float h = 34f;
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height - 110f;

            DrawRect(new Rect(x, y, w * 0.5f, h), WaterColor);
            DrawRect(new Rect(x + w * 0.5f, y, w * 0.5f, h), EarthColor);
            DrawRectBorder(new Rect(x, y, w, h), new Color(0f, 0f, 0f, 0.75f), 1f);

            float cell = w / 7f;
            for (int i = 0; i <= 7; i++)
            {
                float tickX = x + i * cell;
                float tickH = i == 3 || i == 4 ? h : h * 0.5f;
                DrawRect(new Rect(tickX - 1f, y + (h - tickH), 2f, tickH), new Color(0f, 0f, 0f, 0.7f));
            }

            float centerX = x + w * 0.5f;
            float markerX = centerX + Mathf.Clamp(envLevel, -3, 3) / 3f * (w * 0.5f);
            DrawRect(new Rect(markerX - 2f, y - 4f, 4f, h + 8f), MarkerColor);
            DrawRect(new Rect(markerX - 10f, y - 22f, 20f, 18f), new Color(0f, 0f, 0f, 0.75f));
            GUI.Label(new Rect(markerX - 20f, y - 22f, 40f, 18f), FormatLevel(envLevel), axisStyle);

            GUI.Label(new Rect(x + 6f, y + 7f, 70f, 20f), "Nước", axisStyle);
            GUI.Label(new Rect(x + w - 76f, y + 7f, 70f, 20f), "Đất", axisStyle);
        }

        void DrawReclaimTimer()
        {
            if (!reclaimActive || flow == null || flow.State != BattleState.Fighting) return;

            const float w = 420f;
            const float h = 30f;
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height - 110f - h - 8f;
            float remaining = Mathf.Max(0f, flow.ReclaimRemaining);

            DrawRect(new Rect(x, y, w, h), new Color(0f, 0f, 0f, 0.78f));
            Color prev = GUI.color;
            GUI.color = remaining <= 10f ? TimerWarn : TimerOk;
            GUI.Label(new Rect(x, y, w, h),
                $"GIÀNH LẠI MÔI TRƯỜNG  {Mathf.CeilToInt(remaining)}s", timerStyle);
            GUI.color = prev;
        }

        void DrawCooldownIcons()
        {
            if (player == null)
            {
                player = FindFirstObjectByType<PlayerController>();
                if (player == null) return;
            }
            SkillCaster skills = player.Skills;
            if (skills == null) return;

            const float size = 56f;
            const float gap = 10f;
            const float margin = 24f;
            float y = Screen.height - size - margin;
            DrawSlot(skills, SkillSlot.E2, Screen.width - margin - size, y, size);
            DrawSlot(skills, SkillSlot.E1, Screen.width - margin - size * 2f - gap, y, size);
        }

        void DrawSlot(SkillCaster skills, SkillSlot slot, float x, float y, float size)
        {
            string key = slot == SkillSlot.E1 ? "E" : "R";
            float remaining = skills.CooldownRemaining(slot);
            SkillDefinition def = skills.Get(slot);
            float total = def != null && def.cooldown > 0f ? def.cooldown : 8f;

            DrawRect(new Rect(x, y, size, size), IconBg);
            if (remaining > 0f)
            {
                float frac = Mathf.Clamp01(remaining / total);
                DrawRect(new Rect(x, y + size * (1f - frac), size, size * frac), new Color(0f, 0f, 0f, 0.62f));
            }
            DrawRectBorder(new Rect(x, y, size, size), remaining > 0f ? DimBorder : LitBorder, 1.5f);
            GUI.Label(new Rect(x, y + 6f, size, 26f), key, keyStyle);
            if (remaining > 0f)
                GUI.Label(new Rect(x, y + size - 24f, size, 22f), remaining.ToString("F1"), cdStyle);
        }

        static string FormatLevel(int level) => level > 0 ? "+" + level : level.ToString();

        static void DrawRect(Rect rect, Color color)
        {
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        static void DrawRectBorder(Rect rect, Color color, float thickness)
        {
            DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 1f, 1f, 0.95f) }
            };
            timerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            keyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 1f, 1f, 0.95f) }
            };
            cdStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.85f, 0.35f, 1f) }
            };
            axisStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
        }
    }
}
