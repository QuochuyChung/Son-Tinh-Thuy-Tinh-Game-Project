using SonTinhThuyTinh.Dialogue;
using SonTinhThuyTinh.Player;
using UnityEngine;

namespace SonTinhThuyTinh.Quest
{
    // The runaway horse on the summit (HorseQuest Cleared): E next to it rings the keeper's bell, the horse calms down and runs home (Calmed).
    // The horse is skittish: while the player runs flat out (Shift) within spookDistance it rears up and E does nothing until the player
    // has slowed down for a moment. Sits on a child object of the horse that carries the Interactable.
    [RequireComponent(typeof(Interactable))]
    public class HorseSummit : MonoBehaviour
    {
        [SerializeField] HorseStall horse;
        [SerializeField] DialogueRunner runner;
        [SerializeField] DialogueSequence rung;
        [SerializeField] float spookDistance = 13f;
        [SerializeField] float calmDownAfter = 1.5f;

        Interactable interactable;
        PlayerController player;
        bool spooked;
        float calmAt, nextWarningAt;

        void Awake()
        {
            interactable = GetComponent<Interactable>();
            interactable.Interacted.AddListener(Ring);
        }

        void OnDisable() { if (interactable != null) interactable.enabled = false; }

        void Update()
        {
            bool ready = HorseQuest.State == HorseQuestState.Cleared;
            if (!ready) { interactable.enabled = false; return; }

            if (player == null || !player.isActiveAndEnabled)
            {
                player = null;
                foreach (PlayerController c in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
                    if (c.isActiveAndEnabled) { player = c; break; }
                if (player == null) return;
            }

            Vector3 d = player.transform.position - transform.position; d.y = 0f;
            if (d.magnitude < spookDistance && player.IsSprinting)
            {
                spooked = true;
                calmAt = Time.time + calmDownAfter;
                if (horse != null) horse.LookUp();
                if (Time.time >= nextWarningAt)
                {
                    GiftTracker.Announce("Ngựa hoảng sợ vì tiếng chạy!\nThả phím <b>Shift</b>, đi chậm lại gần nó");
                    nextWarningAt = Time.time + 6f;
                }
            }
            else if (spooked && Time.time >= calmAt) spooked = false;

            interactable.enabled = !spooked;
        }

        void Ring()
        {
            if (HorseQuest.State != HorseQuestState.Cleared) return;
            QuestDialogue.Play(runner, rung, () => HorseQuest.Set(HorseQuestState.Calmed));
        }
    }
}
