using SonTinhThuyTinh.Dialogue;
using SonTinhThuyTinh.Player;
using UnityEngine;

namespace SonTinhThuyTinh.Quest
{
    // Plays a conversation once (per playthrough) when the player comes within `radius` of this object: the narration before the
    // crocodile's island, the first sight of the lighthouse, and so on.
    public class SeaIntroTrigger : MonoBehaviour
    {
        [SerializeField] DialogueRunner runner;
        [SerializeField] DialogueSequence sequence;
        [SerializeField] string key = "intro";
        [SerializeField] float radius = 24f;

        PlayerController player;

        void Update()
        {
            if (sequence == null || SeaQuest.IntroSeen(key) || QuestDialogue.IsPlaying) return;
            if (player == null || !player.isActiveAndEnabled)
            {
                player = null;
                foreach (PlayerController c in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
                    if (c.isActiveAndEnabled) { player = c; break; }
                if (player == null) return;
            }
            Vector3 d = player.transform.position - transform.position; d.y = 0f;
            if (d.magnitude > radius || player.Health.IsDead) return;
            SeaQuest.MarkIntroSeen(key);
            QuestDialogue.Play(runner, sequence);
        }
    }
}
