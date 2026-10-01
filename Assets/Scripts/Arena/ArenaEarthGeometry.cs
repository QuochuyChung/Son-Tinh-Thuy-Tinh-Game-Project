using UnityEngine;
using UnityEngine.Playables;
using SonTinhThuyTinh.Combat.Environment;

namespace SonTinhThuyTinh.Arena
{
    [DisallowMultipleComponent]
    public sealed class ArenaEarthGeometry : MonoBehaviour
    {
        [Tooltip("PlayableDirector chạy TimelineAsset geometry Đất 0→3 (3 ring trồi + nước rút về rìa) — [tune].")]
        [SerializeField] private PlayableDirector director;

        [Tooltip("Số giây geometry chạy mỗi bậc Đất — kiểm thử 10–15s/bậc — [tune].")]
        [SerializeField] private float secondsPerStep = 12.5f;

        [Tooltip("Bậc Đất cao nhất tương ứng duration timeline (level +3) — [tune].")]
        [SerializeField] private int maxEarthLevel = 3;

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
                float t01 = Mathf.Clamp01(Mathf.Max(0, level) / (float)Mathf.Max(1, maxEarthLevel));
                targetTime = t01 * duration;
            }
            if (director == null || director.playableAsset == null)
            {
                return;
            }
            if (Mathf.Abs((float)director.time - targetTime) > 0.0005f)
            {
                float duration = Duration();
                float rate = duration / (Mathf.Max(1, maxEarthLevel) * Mathf.Max(0.01f, secondsPerStep));
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
            float t01 = Mathf.Clamp01(Mathf.Max(0, level) / (float)Mathf.Max(1, maxEarthLevel));
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
