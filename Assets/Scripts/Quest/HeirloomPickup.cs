using UnityEngine;

namespace SonTinhThuyTinh.Quest
{
    // The keeper's bronze bell on the mountain path. Hidden until the keeper asks for it (HorseQuest Accepted), then it glows, bobs and
    // has a tall light beam over it so it can be found from afar; E picks it up (HasBell). The visual is a placeholder cube: replace the
    // "Visual" child (or the whole prefab) with the real bell model later, nothing else depends on it.
    [RequireComponent(typeof(Interactable))]
    public class HeirloomPickup : MonoBehaviour
    {
        [SerializeField] string itemName = "Chuông đồng gia truyền";
        [SerializeField] GameObject content;   // visual + glow + beam: shown only while the bell is waiting to be found
        [SerializeField] Transform visual;
        [SerializeField] float bobHeight = 0.12f;
        [SerializeField] float spinSpeed = 50f;

        Interactable interactable;
        Vector3 rest;

        void Awake()
        {
            interactable = GetComponent<Interactable>();
            interactable.Interacted.AddListener(PickUp);
            if (visual != null) rest = visual.localPosition;
        }

        void OnEnable() { HorseQuest.Changed += Refresh; Refresh(HorseQuest.State); }
        void OnDisable() => HorseQuest.Changed -= Refresh;

        void Refresh(HorseQuestState state)
        {
            bool waiting = state == HorseQuestState.Accepted;
            if (content != null) content.SetActive(waiting);
            interactable.enabled = waiting;
        }

        void Update()
        {
            if (visual == null || !visual.gameObject.activeInHierarchy) return;
            visual.localPosition = rest + Vector3.up * (Mathf.Sin(Time.time * 2f) * bobHeight);
            visual.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
        }

        void PickUp()
        {
            if (HorseQuest.State != HorseQuestState.Accepted) return;
            HorseQuest.Set(HorseQuestState.HasBell);
            GiftTracker.Announce($"Đã nhặt <b>{itemName}</b>\nMang về cho người chăn ngựa");
        }
    }
}
