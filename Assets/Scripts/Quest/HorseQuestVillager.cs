using SonTinhThuyTinh.Dialogue;
using UnityEngine;

namespace SonTinhThuyTinh.Quest
{
    // The old horse keeper beside his stable. Talking to him (E) moves HorseQuest along:
    // NotStarted -> asks for his bronze bell (Accepted); Accepted -> a reminder; HasBell -> thanks, gives Ngựa Chín Hồng Mao (the gift
    // banner says so) and the horse leaves the stable (Completed); Completed -> a friendly line.
    [RequireComponent(typeof(Interactable))]
    public class HorseQuestVillager : MonoBehaviour
    {
        [SerializeField] DialogueRunner runner;
        [SerializeField] DialogueSequence offer;
        [SerializeField] DialogueSequence reminder;
        [SerializeField] DialogueSequence thanks;
        [SerializeField] DialogueSequence afterwards;
        [SerializeField] GiftItem horseGift;
        [SerializeField] HorseStall horse;
        [Tooltip("Turns towards the player while talking.")]
        [SerializeField] Transform body;

        void Awake() => GetComponent<Interactable>().Interacted.AddListener(Talk);

        void Start()
        {
            // the horse may already be ours (e.g. a test that collected the gift directly): never give it twice
            if (horseGift != null && GiftTracker.Has(horseGift)) HorseQuest.Set(HorseQuestState.Completed);
            // a state left from an earlier game whose gifts were cleared: the errand starts over
            else if (horseGift != null && HorseQuest.State == HorseQuestState.Completed) HorseQuest.Reset();
        }

        void Talk()
        {
            FacePlayer();
            switch (HorseQuest.State)
            {
                case HorseQuestState.NotStarted:
                    QuestDialogue.Play(runner, offer, () => HorseQuest.Set(HorseQuestState.Accepted));
                    break;
                case HorseQuestState.Accepted:
                    QuestDialogue.Play(runner, reminder);
                    break;
                case HorseQuestState.HasBell:
                    if (horse != null) horse.LookUp();
                    QuestDialogue.Play(runner, thanks, GiveHorse);
                    break;
                default:
                    QuestDialogue.Play(runner, afterwards);
                    break;
            }
        }

        void GiveHorse()
        {
            HorseQuest.Set(HorseQuestState.Completed);
            GiftTracker.Collect(horseGift);   // the gift banner: "Bạn đã nhận được Ngựa chín hồng mao"
            if (horse != null) horse.Leave();  // gone from the stable right away (and on every later visit, HorseStall.Start)
        }

        void FacePlayer()
        {
            var player = FindAnyObjectByType<Player.PlayerController>();
            if (player == null || body == null) return;
            Vector3 d = player.transform.position - body.position; d.y = 0f;
            if (d.sqrMagnitude > 0.01f) body.rotation = Quaternion.LookRotation(d);
        }
    }
}
