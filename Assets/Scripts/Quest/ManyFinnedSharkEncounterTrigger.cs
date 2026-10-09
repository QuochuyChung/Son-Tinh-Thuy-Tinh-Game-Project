using SonTinhThuyTinh.Flow;
using SonTinhThuyTinh.Player;
using UnityEngine;

namespace SonTinhThuyTinh.Quest
{
    // Map 9 Vay is earned by defeating it. Touching the swimming model starts the
    // dedicated arena instead of immediately adding the gift to the inventory.
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public sealed class ManyFinnedSharkEncounterTrigger : MonoBehaviour
    {
        [SerializeField] GiftItem gift;
        [SerializeField] string battleScene = SceneNames.ManyFinnedSharkBattle;
        [SerializeField, Min(0f)] float returnClearance = 2.5f;

        bool entering;

        public GiftItem Gift => gift;

        void Reset() => GetComponent<Collider>().isTrigger = true;

        void Start()
        {
            if (GiftTracker.Has(gift)) gameObject.SetActive(false);
        }

        void OnTriggerEnter(Collider other)
        {
            if (entering || SceneLoader.IsLoading || GiftTracker.Has(gift) || !GameSession.CanStartEncounter) return;
            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player == null || player.Health.IsDead) return;

            entering = true;
            Vector3 away = player.transform.position - transform.position;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = -transform.forward;
            away.Normalize();

            float triggerRadius = 0f;
            if (TryGetComponent(out SphereCollider sphere)) triggerRadius = sphere.radius;
            Vector3 returnPosition = transform.position + away * (triggerRadius + returnClearance);
            returnPosition.y = player.transform.position.y;
            Quaternion returnRotation = Quaternion.LookRotation(-away, Vector3.up);

            GameSession.BeginEncounter(SceneNames.ThuyTinhMap, returnPosition, returnRotation);
            SceneLoader.Load(battleScene);
        }
    }
}
