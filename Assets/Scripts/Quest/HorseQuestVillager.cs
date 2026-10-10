using SonTinhThuyTinh.Dialogue;
using UnityEngine;

namespace SonTinhThuyTinh.Quest
{
    // The old horse keeper beside his stable. Talking to him (E) moves HorseQuest along:
    // NotStarted -> tells how the horse bolted and hands over his bronze bell (Accepted); Accepted / Cleared -> a reminder;
    // Calmed (the bell was rung on the summit, the horse is back in the stable) -> thanks, gives Ngựa Chín Hồng Mao (the gift banner says
    // so) and the horse leaves the stable (Completed); Completed -> a friendly line.
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
        }

        void Talk()
        {
            FacePlayer();
            switch (HorseQuest.State)
            {
                case HorseQuestState.NotStarted:
                    QuestDialogue.Play(runner, offer, Accept);
                    break;
                case HorseQuestState.Accepted:
                case HorseQuestState.Cleared:
                    QuestDialogue.Play(runner, reminder);
                    break;
                case HorseQuestState.Calmed:
                    if (horse != null) horse.LookUp();
                    QuestDialogue.Play(runner, thanks, GiveHorse);
                    break;
                default:
                    QuestDialogue.Play(runner, afterwards);
                    break;
            }
        }

        void Accept()
        {
            HorseQuest.Set(HorseQuestState.Accepted);
            GiftTracker.Announce("Đã nhận <b>Chuông đồng gia truyền</b>\nĐuổi theo ngựa lên đường núi sau làng");
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
