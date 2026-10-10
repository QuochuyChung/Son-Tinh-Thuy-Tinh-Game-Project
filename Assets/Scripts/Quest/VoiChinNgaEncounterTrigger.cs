using SonTinhThuyTinh.Dialogue;
using SonTinhThuyTinh.Flow;
using SonTinhThuyTinh.Player;
using UnityEngine;

namespace SonTinhThuyTinh.Quest
{
    public static class VoiChinNgaQuest
    {
        public static bool IntroSeen { get; private set; }

        public static void MarkIntroSeen() => IntroSeen = true;
        public static void Reset() => IntroSeen = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => Reset();
    }

    // The elephant is a challenge, not a normal pickup. Touching it moves Son Tinh to
    // the dedicated arena; the gift is awarded by the battle controller only on victory.
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public class VoiChinNgaEncounterTrigger : MonoBehaviour
    {
        [SerializeField] GiftItem gift;
        [SerializeField] string battleScene = SceneNames.VoiChinNgaBattle;
        [SerializeField, Min(0f)] float returnClearance = 2.5f;
        [Header("Story")]
        [SerializeField] DialogueRunner dialogueRunner;
        [SerializeField] DialogueSequence introDialogue;

        bool entering;
        DialogueSequence runtimeIntro;

        public GiftItem Gift => gift;

        void Reset() => GetComponent<Collider>().isTrigger = true;

        void Start()
        {
            if (GiftTracker.Has(gift))
            {
                gameObject.SetActive(false);
                return;
            }
            if (dialogueRunner == null) dialogueRunner = FindFirstObjectByType<DialogueRunner>(FindObjectsInactive.Include);
            if (introDialogue == null) runtimeIntro = BuildRuntimeIntro();
        }

        void OnDestroy()
        {
            if (runtimeIntro != null) Destroy(runtimeIntro);
        }

        void OnTriggerEnter(Collider other)
        {
            if (entering || SceneLoader.IsLoading || QuestDialogue.IsPlaying || GiftTracker.Has(gift) || !GameSession.CanStartEncounter) return;
            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player == null || player.Health.IsDead) return;

            Vector3 away = player.transform.position - transform.position;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = -transform.forward;
            away.Normalize();

            float triggerRadius = 0f;
            if (TryGetComponent(out SphereCollider sphere)) triggerRadius = sphere.radius;
            Vector3 returnPosition = transform.position + away * (triggerRadius + returnClearance);
            returnPosition.y = player.transform.position.y;
            Quaternion returnRotation = Quaternion.LookRotation(-away, Vector3.up);

            entering = true;
            void EnterBattle()
            {
                VoiChinNgaQuest.MarkIntroSeen();
                GameSession.BeginEncounter(SceneNames.SonTinhMap, returnPosition, returnRotation);
                SceneLoader.Load(battleScene);
            }

            if (VoiChinNgaQuest.IntroSeen)
            {
                EnterBattle();
                return;
            }

            QuestDialogue.Play(dialogueRunner, introDialogue != null ? introDialogue : runtimeIntro, EnterBattle);
        }

        static DialogueSequence BuildRuntimeIntro()
        {
            return DialogueSequence.CreateRuntime(
                new DialogueLine
                {
                    speaker = "Dẫn chuyện",
                    text = "Giữa thung lũng đá, Voi Chín Ngà đứng chắn lối. Mỗi nhịp thở của linh tượng làm mặt đất khẽ rung chuyển."
                },
                new DialogueLine
                {
                    speaker = "Voi Chín Ngà",
                    text = "Kẻ phàm nào dám bước vào lãnh địa của ta?"
                },
                new DialogueLine
                {
                    speaker = "Sơn Tinh",
                    text = "Ta là Sơn Tinh. Ta đến tìm Voi Chín Ngà làm sính lễ dâng Vua Hùng."
                },
                new DialogueLine
                {
                    speaker = "Voi Chín Ngà",
                    text = "Sính lễ không dành cho kẻ chỉ biết khoe sức. Hãy chứng minh ngươi đủ bản lĩnh bảo vệ Mị Nương."
                },
                new DialogueLine
                {
                    speaker = "Dẫn chuyện",
                    text = "Chín chiếc ngà rực sáng. Linh tượng hạ thấp thân mình, sẵn sàng tung ra những đòn rung núi chuyển rừng."
                },
                new DialogueLine
                {
                    speaker = "Sơn Tinh",
                    text = "Ta chấp nhận thử thách. Xin hãy xuất chiêu!"
                });
        }
    }
}
