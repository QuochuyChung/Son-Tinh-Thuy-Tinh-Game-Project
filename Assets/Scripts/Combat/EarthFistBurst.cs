using System.Collections.Generic;
using UnityEngine;

namespace SonTinhThuyTinh.Combat
{
    // The look of Sơn Tinh's combo finisher (Địa Chấn Quyền): where his fist hits the ground a burst of dust goes up and a fan of short
    // stone spikes stabs out of the ground ahead of him, row after row, then sinks again. Only the look: the attack's own hit box
    // (Attack_EarthFist) does the damage. Spawned as the attack's swing effect, facing the way he strikes.
    public class EarthFistBurst : MonoBehaviour
    {
        [SerializeField] GameObject spireModel;
        [SerializeField] GameObject dustPrefab;
        [Tooltip("Rows of spikes going out from the fist, and how far the last one is.")]
        [SerializeField] int rows = 3;
        [SerializeField] float reach = 4.2f;
        [Tooltip("Half angle of the fan, degrees.")]
        [SerializeField] float spread = 28f;
        [SerializeField] float rowDelay = 0.05f;
        [SerializeField] float riseTime = 0.08f;
        [SerializeField] float holdTime = 0.45f;
        [SerializeField] float sinkTime = 0.3f;

        class Spike
        {
            public Transform transform;
            public Vector3 ground;
            public Quaternion rotation;
            public float height;
            public float at;
        }

        readonly List<Spike> spikes = new();
        float elapsed;

        void Start()
        {
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            float scale = transform.localScale.x;
            Vector3 origin = transform.position + forward * (0.7f * scale);   // the swing effect stands at his feet: the fist lands ahead
            RockSkillUtil.Dust(dustPrefab, origin, 1.2f * scale);
            if (spireModel == null) return;

            for (int row = 0; row < rows; row++)
            {
                float distance = Mathf.Lerp(1.1f, reach, rows > 1 ? row / (float)(rows - 1) : 1f) * scale;
                int count = 2 + row;                                   // the fan widens: 2, 3, 4 spikes
                for (int i = 0; i < count; i++)
                {
                    float angle = count > 1 ? Mathf.Lerp(-spread, spread, i / (float)(count - 1)) : 0f;
                    angle += Random.Range(-5f, 5f);
                    Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * forward;
                    Vector3 at = origin + dir * (distance + Random.Range(-0.2f, 0.2f));
                    at.y = RockSkillUtil.GroundY(at, origin.y);
                    // leaning out along the blast, a little bigger further out
                    Quaternion rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(28f + Random.Range(-6f, 6f), Random.Range(-30f, 30f), Random.Range(-8f, 8f));
                    Transform model = Instantiate(spireModel, at, rotation).transform;   // not under this object: its scale is the swing scale
                    model.SetParent(transform, true);
                    model.localScale = Vector3.one * Mathf.Lerp(0.32f, 0.5f, row / Mathf.Max(1f, rows - 1f)) * Random.Range(0.85f, 1.15f) / Mathf.Max(scale, 0.01f);
                    foreach (Collider c in model.GetComponentsInChildren<Collider>()) Destroy(c);
                    var spike = new Spike { transform = model, ground = at, rotation = rotation, at = row * rowDelay };
                    spike.height = Mathf.Max(0.3f, RockSkillUtil.LocalBounds(model).size.y * scale);
                    spikes.Add(spike);
                    Place(spike, 0f);
                }
            }
        }

        void Update()
        {
            elapsed += Time.deltaTime;
            foreach (Spike s in spikes)
            {
                float t = elapsed - s.at;
                float up;
                if (t < 0f) up = 0f;
                else if (t < riseTime) up = 1f - (1f - t / riseTime) * (1f - t / riseTime);
                else if (t < riseTime + holdTime) up = 1f;
                else up = 1f - Mathf.Clamp01((t - riseTime - holdTime) / sinkTime);
                Place(s, up);
            }
        }

        void Place(Spike s, float up)
        {
            s.transform.SetPositionAndRotation(s.ground + Vector3.down * (s.height * (1.05f - up)), s.rotation);
            s.transform.gameObject.SetActive(up > 0.001f);
        }
    }
}
