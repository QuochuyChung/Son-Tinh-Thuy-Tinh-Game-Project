using UnityEngine;

namespace SonTinhThuyTinh.Combat.AI
{
    [CreateAssetMenu(menuName = "SonTinhThuyTinh/Boss Aggression Config", fileName = "BossAggression_")]
    public sealed class BossAggressionConfig : ScriptableObject
    {
        [Tooltip("Giây giãn cách giữa 2 đòn của boss (§8.3: aggression dùng chung cho 2 boss).")]
        [Min(0f)] public float attackGapSeconds = 2f;

        [Tooltip("Xác suất ra E1 (Thành Lũy) ở slot đòn kế tiếp.")]
        [Range(0f, 1f)] public float e1Chance = 0.2f;

        [Tooltip("Xác suất ra E2 (Đạp Núi) ở slot đòn kế tiếp.")]
        [Range(0f, 1f)] public float e2Chance = 0.15f;

        [Tooltip("Hệ số rút ngắn nhịp đòn ở P3 (tăng tốc — §6).")]
        [Range(0.1f, 1f)] public float p3TempoMultiplier = 0.75f;

        [Tooltip("Tốc độ đuổi người chơi (m/s), nhân thêm env speed multiplier.")]
        [Min(0.1f)] public float moveSpeed = 3f;

        [Tooltip("Khoảng cách dừng chân để ra đòn cận chiến (m).")]
        [Min(0.5f)] public float attackRange = 2.6f;

        [Tooltip("Telegraph trước khi boss cast Đại Pháp scripted (giây — design §6: 1.5s).")]
        [Min(0f)] public float fTelegraphSeconds = 1.5f;

        [Tooltip("Cửa sổ kiệt sức sau Đại Pháp: không ra đòn, quỳ placeholder (giây — design §7: 2.5s).")]
        [Min(0f)] public float exhaustionSeconds = 2.5f;
    }
}
