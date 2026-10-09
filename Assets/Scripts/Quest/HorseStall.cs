using UnityEngine;

namespace SonTinhThuyTinh.Quest
{
    // Ngựa Chín Hồng Mao in its stable: grazes at the hay trough (EatGrass) and lifts its head (LookUp, then Idle) when the player comes
    // close or hears the keeper's bell, going back to eating once the player has walked off. Once the keeper has given it away
    // (HorseQuest Completed) it is gone from the stable, also when the map is loaded again. It never follows anyone and cannot be ridden.
    public class HorseStall : MonoBehaviour
    {
        [SerializeField] Animator animator;
        [SerializeField] float noticeDistance = 6f;
        [SerializeField] float forgetDistance = 11f;
        [SerializeField] float backToEatingAfter = 6f;

        Transform player;
        bool watching;
        float calmSince = -1f;

        void Awake() => HorseQuest.Changed += OnQuestChanged;
        void OnDestroy() => HorseQuest.Changed -= OnQuestChanged;
        void Start() => OnQuestChanged(HorseQuest.State);

        // also catches the keeper marking the quest done on load when the gift was already owned (HorseQuestVillager.Start)
        void OnQuestChanged(HorseQuestState state)
        {
            if (state == HorseQuestState.Completed && this != null) gameObject.SetActive(false);
        }

        // the keeper has handed it over: it leaves the stable (the trough and the stable stay)
        public void Leave() => gameObject.SetActive(false);

        public void LookUp()
        {
            if (animator == null || !isActiveAndEnabled) return;
            animator.ResetTrigger("Eat");
            animator.SetTrigger("LookUp");
            watching = true; calmSince = -1f;
        }

        void Update()
        {
            if (player == null)
            {
                var pc = FindAnyObjectByType<Player.PlayerController>();
                if (pc == null) return;
                player = pc.transform;
            }
            Vector3 d = player.position - transform.position; d.y = 0f;
            float distance = d.magnitude;
            if (!watching && distance < noticeDistance) LookUp();
            else if (watching && distance > forgetDistance)
            {
                if (calmSince < 0f) calmSince = Time.time;
                else if (Time.time - calmSince > backToEatingAfter)
                {
                    animator.ResetTrigger("LookUp");
                    animator.SetTrigger("Eat");
                    watching = false; calmSince = -1f;
                }
            }
            else calmSince = -1f;
        }
    }
}
