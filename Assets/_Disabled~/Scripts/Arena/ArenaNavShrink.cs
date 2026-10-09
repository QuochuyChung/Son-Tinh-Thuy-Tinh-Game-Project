using UnityEngine;
using UnityEngine.AI;
using SonTinhThuyTinh.Combat.Environment;

namespace SonTinhThuyTinh.Arena
{
    [DisallowMultipleComponent]
    public sealed class ArenaNavShrink : MonoBehaviour
    {
        [Tooltip("Bán kính vùng đi được của arena khô — NavMesh thực đo ≈12.17 (mép trong Rim 12.675 trừ agent radius 0.5) — [tune].")]
        [SerializeField] private float edgeRadius = 12.175f;

        [Tooltip("Số mét bán kính co mỗi bậc Nước (level 0 → -3) — [tune].")]
        [SerializeField] private float shrinkPerStep = 1.5f;

        [Tooltip("Thời gian (giây) co 1 bậc, khớp timeline geometry 10–15s — [tune]. Nhỏ hơn hoặc bằng 0 để áp dụng tức thì.")]
        [SerializeField] private float tweenSeconds = 12f;

        [Tooltip("Độ rộng chêm thêm mỗi đầu của dải carve so với dải cần xóa — [tune].")]
        [SerializeField] private float carveOverlap = 0.5f;

        private float currentRadius;
        private float targetRadius;
        private int lastLevel = int.MinValue;
        private bool hasChildren;
        private NavMeshObstacle[] obstacles;

        public float WalkableRadius => currentRadius;

        public float TargetRadius => targetRadius;

        private void Awake()
        {
            currentRadius = edgeRadius;
            targetRadius = edgeRadius;
            int count = transform.childCount;
            hasChildren = count > 0;
            if (hasChildren)
            {
                obstacles = new NavMeshObstacle[count];
                for (int i = 0; i < count; i++)
                {
                    obstacles[i] = transform.GetChild(i).GetComponent<NavMeshObstacle>();
                }
            }
            Place();
        }

        private void Update()
        {
            EnvironmentDirector env = EnvironmentDirector.Instance;
            int level = env != null ? env.Level : 0;
            if (level != lastLevel)
            {
                lastLevel = level;
                targetRadius = edgeRadius - shrinkPerStep * Mathf.Max(0, -level);
            }
            if (Mathf.Abs(currentRadius - targetRadius) > 0.0005f)
            {
                if (tweenSeconds > 0f)
                {
                    float rate = shrinkPerStep / tweenSeconds;
                    currentRadius = Mathf.MoveTowards(currentRadius, targetRadius, rate * Time.deltaTime);
                }
                else
                {
                    currentRadius = targetRadius;
                }
                Place();
            }
        }

        private void Place()
        {
            if (!hasChildren)
            {
                return;
            }
            float bandWidth = edgeRadius - currentRadius;
            bool shrunk = bandWidth > 0.01f;
            float ringRadius = (currentRadius + edgeRadius) * 0.5f;
            int count = obstacles.Length;
            float carve = bandWidth * 0.5f + carveOverlap;
            if (count > 0)
            {
                float minCarve = ringRadius * Mathf.Sin(Mathf.PI / count) + 0.05f;
                if (carve < minCarve)
                {
                    carve = minCarve;
                }
            }
            for (int i = 0; i < count; i++)
            {
                Transform child = obstacles[i] != null ? obstacles[i].transform : transform.GetChild(i);
                if (shrunk)
                {
                    float angle = (float)i / count * Mathf.PI * 2f;
                    child.localPosition = new Vector3(Mathf.Cos(angle) * ringRadius, 0f, Mathf.Sin(angle) * ringRadius);
                    if (obstacles[i] != null && Mathf.Abs(obstacles[i].radius - carve) > 0.001f)
                    {
                        obstacles[i].radius = carve;
                    }
                }
                if (child.gameObject.activeSelf != shrunk)
                {
                    child.gameObject.SetActive(shrunk);
                }
            }
        }
    }
}
