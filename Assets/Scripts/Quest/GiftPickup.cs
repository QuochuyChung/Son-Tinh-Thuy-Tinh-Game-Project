using SonTinhThuyTinh.Player;
using UnityEngine;
using UnityEngine.Events;

namespace SonTinhThuyTinh.Quest
{
    // Trigger zone that gives a gift when the player walks in. Place it where the challenge ends
    // (next to the elephant, inside the shrine, ...). Disable it until the challenge is cleared if needed.
    [RequireComponent(typeof(Collider))]
    public class GiftPickup : MonoBehaviour
    {
        [SerializeField] GiftItem gift;
        [Tooltip("Optional child that bobs and spins so the pickup reads as collectible. Leave empty for a still model.")]
        [SerializeField] Transform visual;
        [SerializeField] float bobHeight = 0.15f;
        [SerializeField] float bobSpeed = 2f;
        [Tooltip("Degrees per second.")]
        [SerializeField] float spinSpeed = 60f;
        [SerializeField] GameObject collectEffect;
        [SerializeField] AudioClip collectSound;
        [Tooltip("For level logic, e.g. opening a gate once the gift is taken.")]
        [SerializeField] UnityEvent collected;

        Vector3 visualRestPosition;

        public GiftItem Gift => gift;

        void Reset() => GetComponent<Collider>().isTrigger = true;

        void Start()
        {
            // Coming back to a map after the gift was already taken.
            if (GiftTracker.Has(gift)) gameObject.SetActive(false);
            if (visual != null) visualRestPosition = visual.localPosition;
        }

        void Update()
        {
            if (visual == null) return;

            visual.localPosition = visualRestPosition + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobHeight);
            visual.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<PlayerController>() == null) return;
            if (!GiftTracker.Collect(gift)) return;

            if (collectEffect != null) Instantiate(collectEffect, transform.position, Quaternion.identity);
            if (collectSound != null) AudioSource.PlayClipAtPoint(collectSound, transform.position);
            collected.Invoke();
            gameObject.SetActive(false);
        }
    }
}
