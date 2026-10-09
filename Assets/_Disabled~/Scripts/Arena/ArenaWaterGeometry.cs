using UnityEngine;
using UnityEngine.Playables;
using SonTinhThuyTinh.Combat.Environment;

namespace SonTinhThuyTinh.Arena
{
    [DisallowMultipleComponent]
    public sealed class ArenaWaterGeometry : MonoBehaviour
    {
        [Tooltip("PlayableDirector chạy TimelineAsset geometry Nước 0→3 (plane nâng + bán kính co về giữa) — [tune].")]
        [SerializeField] private PlayableDirector director;

        [Tooltip("Số giây geometry chạy mỗi bậc Nước — kiểm thử 10–15s/bậc — [tune].")]
        [SerializeField] private float secondsPerStep = 12.5f;

        [Tooltip("Bậc Nước cao nhất tương ứng duration timeline (level −3) — [tune].")]
        [SerializeField] private int maxWaterLevel = 3;

        private int lastLevel = int.MinValue;
        private float targetTime;

        private void Start()
        {
            if (director == null || director.playableAsset == null)
            {
                return;
            }
            lastLevel = int.MinValue;
            SyncTarget(true);
        }

        private void Update()
        {
            EnvironmentDirector env = EnvironmentDirector.Instance;
            int level = env != null ? env.Level : 0;
            if (level != lastLevel)
            {
                lastLevel = level;
                float duration = Duration();
                float t01 = Mathf.Clamp01(Mathf.Max(0, -level) / (float)Mathf.Max(1, maxWaterLevel));
                targetTime = t01 * duration;
            }
            if (director == null || director.playableAsset == null)
            {
                return;
            }
            if (Mathf.Abs((float)director.time - targetTime) > 0.0005f)
            {
                float duration = Duration();
                float rate = duration / (Mathf.Max(1, maxWaterLevel) * Mathf.Max(0.01f, secondsPerStep));
                director.time = Mathf.MoveTowards((float)director.time, targetTime, rate * Time.deltaTime);
                director.Evaluate();
            }
        }

        private void SyncTarget(bool force)
        {
            EnvironmentDirector env = EnvironmentDirector.Instance;
            int level = env != null ? env.Level : 0;
            lastLevel = level;
            float duration = Duration();
            float t01 = Mathf.Clamp01(Mathf.Max(0, -level) / (float)Mathf.Max(1, maxWaterLevel));
            targetTime = t01 * duration;
            if (force)
            {
                director.time = 0;
                director.Evaluate();
            }
        }

        private float Duration()
        {
            return director != null && director.playableAsset != null
                ? (float)director.playableAsset.duration
                : 0f;
        }
    }
}
