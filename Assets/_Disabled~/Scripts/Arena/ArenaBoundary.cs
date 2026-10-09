using UnityEngine;

namespace SonTinhThuyTinh.Arena
{
    public sealed class ArenaBoundary : MonoBehaviour
    {
        public static ArenaBoundary Instance { get; private set; }

        [Tooltip("Radius (m) of the invisible cylindrical wall, measured on the XZ plane from this transform.")]
        [SerializeField] float radius = 14f;

        public float Radius => radius;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public Vector3 ClampPosition(Vector3 position)
        {
            Vector3 center = transform.position;
            Vector3 flat = new Vector3(position.x - center.x, 0f, position.z - center.z);
            float distance = flat.magnitude;
            if (distance <= radius || distance <= Mathf.Epsilon) return position;
            Vector3 offset = flat * (radius / distance);
            return new Vector3(center.x + offset.x, position.y, center.z + offset.z);
        }

        public bool IsOutside(Vector3 position)
        {
            Vector3 center = transform.position;
            float dx = position.x - center.x;
            float dz = position.z - center.z;
            return dx * dx + dz * dz > radius * radius;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
