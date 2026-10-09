using System;
using SonTinhThuyTinh.Combat.Environment;
using UnityEngine;

namespace SonTinhThuyTinh.Combat.Boss
{
    public enum BattleState
    {
        Fighting,
        Exhausted,
        Won,
        Lost
    }

    public sealed class BattleFlow : MonoBehaviour
    {
        [Tooltip("Giây giành lại env sau mỗi lần phase lật (điều kiện thua #2).")]
        [SerializeField, Min(1f)] float reclaimSeconds = 60f;

        [Tooltip("Giây placeholder từ lúc boss cạn kiệt đến lúc thắng (cinematic tới sau).")]
        [SerializeField, Min(0f)] float finisherPlaceholderSeconds = 4f;

        public static BattleFlow Instance { get; private set; }

        public BattleState State { get; private set; } = BattleState.Fighting;
        public bool ReclaimActive { get; private set; }
        public float ReclaimRemaining { get; set; }
        public string EndReason { get; private set; } = "";
        public bool IsResolved => State == BattleState.Won || State == BattleState.Lost;

        public event Action<BattleState, string> BattleEnded;
        public event Action<int> ReclaimStarted;
        public event Action ReclaimStopped;

        BossHealth boss;
        Player.PlayerHealth player;
        float winAt = -1f;

        public BossHealth Boss => boss != null ? boss : boss = BossHealth.Instance;
        public Player.PlayerHealth PlayerHealth => player != null ? player : player = FindFirstObjectByType<Player.PlayerHealth>();

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            if (boss == null) boss = BossHealth.Instance;
            if (player == null) player = FindFirstObjectByType<Player.PlayerHealth>();
            if (boss != null)
            {
                boss.PhaseStarted += OnPhaseStarted;
                boss.Exhausted += OnExhausted;
            }
            if (player != null) player.Died += OnPlayerDied;
        }

        void Update()
        {
            if (winAt >= 0f && Time.time >= winAt)
            {
                winAt = -1f;
                End(BattleState.Won, "finisher placeholder");
                return;
            }

            if (!ReclaimActive || State != BattleState.Fighting) return;

            ReclaimRemaining -= Time.deltaTime;
            if (PlayerRelativeLevel() >= 1)
            {
                StopReclaim("đã giành lại env");
                return;
            }
            if (ReclaimRemaining > 0f) return;

            if (PlayerRelativeLevel() <= 0) End(BattleState.Lost, "hết giờ giành lại env");
            else StopReclaim("đã giành lại env");
        }

        void OnPhaseStarted(int phase)
        {
            if (State != BattleState.Fighting) return;
            ReclaimRemaining = reclaimSeconds;
            ReclaimActive = true;
            ReclaimStarted?.Invoke(phase);
        }

        void OnExhausted()
        {
            if (State != BattleState.Fighting) return;
            State = BattleState.Exhausted;
            StopReclaim("boss cạn kiệt");
            if (FinisherPrompt.Instance != null)
            {
                Debug.Log("[BattleFlow] Boss cạn kiệt — chờ FinisherPrompt kết liễu");
                return;
            }
            winAt = finisherPlaceholderSeconds > 0f ? Time.time + finisherPlaceholderSeconds : -2f;
            Debug.Log("[BattleFlow] Boss cạn kiệt — finisher placeholder");
            if (winAt == -2f) End(BattleState.Won, "finisher placeholder");
        }

        void OnPlayerDied()
        {
            if (State == BattleState.Exhausted)
            {
                Debug.Log("[BattleFlow] Người chơi ngã nhưng boss đã cạn kiệt — bỏ qua");
                return;
            }
            End(BattleState.Lost, "hết HP người chơi");
        }

        public void CompleteFinisher(string reason) => End(BattleState.Won, reason);

        void End(BattleState state, string reason)
        {
            if (IsResolved) return;
            State = state;
            EndReason = reason;
            ReclaimActive = false;
            Debug.Log($"[BattleFlow] Kết trận: {state} ({reason})");
            BattleEnded?.Invoke(state, reason);
        }

        void StopReclaim(string reason)
        {
            if (!ReclaimActive) return;
            ReclaimActive = false;
            ReclaimStopped?.Invoke();
            Debug.Log("[BattleFlow] Dừng đếm ngược: " + reason);
        }

        int PlayerRelativeLevel()
        {
            EnvironmentDirector env = EnvironmentDirector.Instance;
            if (env == null) return 1;
            int sign = env.PlayerFaction == EnvFaction.SonTinh ? 1 : -1;
            return env.Level * sign;
        }
    }
}
