using SonTinhThuyTinh.Player;
using SonTinhThuyTinh.Quest;
using UnityEngine;
using UnityEngine.Events;

namespace SonTinhThuyTinh.Flow
{
    // Exit of a map: loads the next scene when the player walks in, optionally only once a quest is complete.
    [RequireComponent(typeof(Collider))]
    public class SceneTransitionTrigger : MonoBehaviour
    {
        [SerializeField] string sceneName;
        [Tooltip("Optional. The exit stays closed until every gift in this quest is collected.")]
        [SerializeField] GiftQuest requiredQuest;
        [Tooltip("Raised when the player arrives before the quest is complete, e.g. to show \"chưa đủ sính lễ\".")]
        [SerializeField] UnityEvent blocked;

        void Reset() => GetComponent<Collider>().isTrigger = true;

        void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<PlayerController>() == null) return;

            if (requiredQuest != null && !requiredQuest.IsComplete)
            {
                blocked.Invoke();
                return;
            }

            SceneLoader.Load(sceneName);
        }
    }
}
