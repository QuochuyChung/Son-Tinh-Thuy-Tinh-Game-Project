using System;
using UnityEngine;

namespace SonTinhThuyTinh.Combat
{
    // Drives a dramatic cluster of 3D mountain peaks erupting upward from beneath the ground,
    // standing tall as rocky terrain during the spell duration, and sinking back into the earth.
    public class RisingMountainZone : MonoBehaviour
    {
        [Serializable]
        public class MountainPeak
        {
            public Transform transform;
            public float targetY = 0f;
            public float delay = 0f;
            [HideInInspector] public Vector3 baseLocalPos;
        }

        public MountainPeak[] peaks;
        public float riseDuration = 0.55f;
        public float totalDuration = 6.0f;
        public float sinkDuration = 0.75f;
        public const float SubmergeDepth = -6.2f;

        float timer;

        void Awake()
        {
            if (peaks == null) return;
            for (int i = 0; i < peaks.Length; i++)
            {
                var p = peaks[i];
                if (p.transform == null) continue;
                p.baseLocalPos = p.transform.localPosition;
                // Pre-submerge below ground
                p.transform.localPosition = new Vector3(p.baseLocalPos.x, SubmergeDepth, p.baseLocalPos.z);
            }
        }

        void Start()
        {
            // Ensure submerged on start
            if (peaks == null) return;
            for (int i = 0; i < peaks.Length; i++)
            {
                var p = peaks[i];
                if (p.transform == null) continue;
                if (p.baseLocalPos == Vector3.zero && p.transform.localPosition.y > SubmergeDepth + 0.1f)
                    p.baseLocalPos = p.transform.localPosition;
                p.transform.localPosition = new Vector3(p.baseLocalPos.x, SubmergeDepth, p.baseLocalPos.z);
            }
        }

        void Update()
        {
            timer += Time.deltaTime;
            if (peaks == null) return;

            float sinkStartTime = Mathf.Max(0.5f, totalDuration - sinkDuration);

            for (int i = 0; i < peaks.Length; i++)
            {
                var p = peaks[i];
                if (p.transform == null) continue;

                if (timer < sinkStartTime)
                {
                    // Rising phase with punchy ease-out
                    float elapsed = timer - p.delay;
                    if (elapsed <= 0f)
                    {
                        p.transform.localPosition = new Vector3(p.baseLocalPos.x, SubmergeDepth, p.baseLocalPos.z);
                        continue;
                    }

                    float t = Mathf.Clamp01(elapsed / riseDuration);
                    // Smooth ease-out cubic
                    float ease = 1f - Mathf.Pow(1f - t, 3f);
                    float curY = Mathf.Lerp(SubmergeDepth, p.targetY, ease);
                    p.transform.localPosition = new Vector3(p.baseLocalPos.x, curY, p.baseLocalPos.z);
                }
                else
                {
                    // Sinking phase: smoothly recedes back into the earth
                    float t = Mathf.Clamp01((timer - sinkStartTime) / sinkDuration);
                    float ease = t * t;
                    float curY = Mathf.Lerp(p.targetY, SubmergeDepth, ease);
                    p.transform.localPosition = new Vector3(p.baseLocalPos.x, curY, p.baseLocalPos.z);
                }
            }
        }
    }
}
