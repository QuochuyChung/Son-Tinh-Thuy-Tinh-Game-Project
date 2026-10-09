using System;
using UnityEngine;

namespace SonTinhThuyTinh.Combat.Environment
{
    public enum EnvFaction
    {
        SonTinh = 0,
        ThuyTinh = 1
    }

    [RequireComponent(typeof(ComboChainTracker))]
    public sealed class EnvironmentDirector : MonoBehaviour
    {
        public const int MinLevel = -3;
        public const int MaxLevel = 3;

        [Tooltip("Sign of Level maps to this faction's side: + = Đất (Sơn Tinh), - = Nước (Thủy Tinh).")]
        [SerializeField] EnvFaction playerFaction = EnvFaction.SonTinh;

        ComboChainTracker chain;

        public static EnvironmentDirector Instance { get; private set; }

        public int Level { get; private set; }
        public ComboChainTracker Chain => chain;
        public EnvFaction PlayerFaction
        {
            get => playerFaction;
            set => playerFaction = value;
        }

        public event Action<int, int> LevelChanged;

        int TowardPlayerSign => playerFaction == EnvFaction.SonTinh ? 1 : -1;
        int TowardBossSign => -TowardPlayerSign;

        void Awake()
        {
            Instance = this;
            chain = GetComponent<ComboChainTracker>();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void AddTowardPlayer(int steps) => Add(steps * TowardPlayerSign);

        public void AddTowardBoss(int steps) => Add(steps * TowardBossSign);

        public void Add(int signedDelta)
        {
            if (signedDelta == 0) return;
            int next = Mathf.Clamp(Level + signedDelta, MinLevel, MaxLevel);
            if (next == Level) return;
            int delta = next - Level;
            Level = next;
            LevelChanged?.Invoke(Level, delta);
        }

        public void RegisterChainHit(bool isHeavy)
        {
            if (chain == null) return;
            chain.Add(isHeavy ? 2 : 1);
            while (chain.TryConsumeStep()) AddTowardPlayer(1);
        }

        public void RegisterPlayerHit()
        {
            if (chain != null) chain.ResetChain();
            AddTowardBoss(1);
        }

        public void RegisterUlt() => AddTowardPlayer(2);

        public void FlipTowardBoss()
        {
            int magnitude = Mathf.Max(2, Mathf.Abs(Level));
            Add(TowardBossSign * magnitude - Level);
        }

        public void AdjustLevel(int delta) => Add(delta);

        public float GetSpeedMultiplier(EnvFaction faction)
        {
            if (Level == 0) return 1f;
            bool favored = Level > 0 ? faction == EnvFaction.SonTinh : faction == EnvFaction.ThuyTinh;
            return favored ? 1f + 0.05f * Mathf.Abs(Level) : 1f - 0.05f * Mathf.Abs(Level);
        }

        public float GetStaminaRegenMultiplier(EnvFaction faction)
        {
            if (Level == 0) return 1f;
            bool favored = Level > 0 ? faction == EnvFaction.SonTinh : faction == EnvFaction.ThuyTinh;
            return favored ? 1f + 0.10f * Mathf.Abs(Level) : 1f;
        }
    }
}
