using SonTinhThuyTinh.Dialogue;
using SonTinhThuyTinh.Player;
using UnityEngine;

namespace SonTinhThuyTinh.Quest
{
    // The two talkers of Map_ThuyTinh (placeholder models for now): the old fisherman at the harbour who gives the errand, and the village
    // elder on the fishing village's island who points the way to the lighthouse and the three guardians.
    //  - Fisherman: NotStarted -> the offer (Accepted); Accepted -> a reminder; Cleared -> thanks the first time, then a friendly word
    //    (with a tip about the deep water).
    //  - Elder: first time -> the story of the lamp; before the lamp is lit -> a reminder; lit -> congratulations.
    [RequireComponent(typeof(Interactable))]
    public class SeaNpc : MonoBehaviour
    {
        public enum Role { Fisherman, Elder }

        public Role NpcRole => role;

        [SerializeField] Role role;
        [SerializeField] DialogueRunner runner;
        [SerializeField] DialogueSequence offer;
        [SerializeField] DialogueSequence reminder;
        [SerializeField] DialogueSequence thanks;
        [SerializeField] DialogueSequence afterwards;
        [Tooltip("Turns towards the player while talking.")]
        [SerializeField] Transform body;

        void Awake() => GetComponent<Interactable>().Interacted.AddListener(Talk);

        void Talk()
        {
            FacePlayer();
            if (role == Role.Fisherman) TalkFisherman(); else TalkElder();
        }

        void TalkFisherman()
        {
            switch (SeaQuest.State)
            {
                case SeaQuestState.NotStarted:
                    PlayerSpawner spawner = FindAnyObjectByType<PlayerSpawner>();
                    GiftItem next = spawner != null ? QuestQuizProgress.NextGift(spawner.Roster.Get(spawner.Character).GiftQuest) : null;
                    if (!QuestQuizProgress.IsUnlocked(next))
                    {
                        QuestQuizProgress.AnnounceLocked(next);
                        return;
                    }
                    QuestDialogue.Play(runner, offer, () =>
                    {
                        SeaQuest.Set(SeaQuestState.Accepted);
                        GiftTracker.Announce("Nhiệm vụ: dẹp bọn ngư tặc ở Bến Thuyền\nBước lên cầu gỗ phía trước");
                    });
                    break;
                case SeaQuestState.Accepted:
                    QuestDialogue.Play(runner, reminder);
                    break;
                default:
                    if (!SeaQuest.IntroSeen("FishermanThanks"))
                    {
                        SeaQuest.MarkIntroSeen("FishermanThanks");
                        QuestDialogue.Play(runner, thanks);
                    }
                    else QuestDialogue.Play(runner, afterwards);
                    break;
            }
        }

        void TalkElder()
        {
            if (SeaQuest.LampLit) QuestDialogue.Play(runner, thanks);
            else if (!SeaQuest.ElderMet)
                QuestDialogue.Play(runner, offer, () =>
                {
                    SeaQuest.MeetElder();
                    GiftTracker.Announce("Nhiệm vụ: lên Hải đăng ở phía đông làng\nĐuổi ma nước và thắp lại ngọn đèn");
                });
            else QuestDialogue.Play(runner, reminder);
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
