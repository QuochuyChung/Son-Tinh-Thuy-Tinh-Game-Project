using UnityEngine;

namespace SonTinhThuyTinh.Quest
{
    // A guide arrow floating over a quest goal: turns slowly and bobs up and down. The mesh points down with its tip at the pivot.
    public class QuestArrow : MonoBehaviour
    {
        [SerializeField] float bobHeight = 0.35f;
        [SerializeField] float bobSpeed = 2.2f;
        [SerializeField] float spinSpeed = 70f;

        Vector3 rest;

        void Awake() => rest = transform.localPosition;

        void Update()
        {
            transform.localPosition = rest + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobHeight);
            transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);
        }
    }
}
