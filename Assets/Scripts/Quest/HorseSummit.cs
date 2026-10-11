using SonTinhThuyTinh.Audio;
using SonTinhThuyTinh.Combat;
using SonTinhThuyTinh.Dialogue;
using SonTinhThuyTinh.Player;
using SonTinhThuyTinh.UI;
using UnityEngine;

namespace SonTinhThuyTinh.Quest
{
    // The runaway horse on the summit (HorseQuest Cleared): E next to it rings the keeper's bell, the horse calms down and runs home (Calmed).
    // The horse is skittish: while the player runs flat out (Shift) within spookDistance it rears up and E does nothing until the player
    // has slowed down for a moment. Keep running (Shift) closer than kickDistance and it lashes out: a kick that costs kickDamage health
    // (never the last point), throws the player back, at most once per kickCooldown. Sits on a child object of the horse that carries
    // the Interactable.
    [RequireComponent(typeof(Interactable))]
    public class HorseSummit : MonoBehaviour
    {
        [SerializeField] HorseStall horse;
        [SerializeField] DialogueRunner runner;
        [SerializeField] DialogueSequence rung;
        [SerializeField] float spookDistance = 13f;
        [SerializeField] float calmDownAfter = 1.5f;
        [Tooltip("Running this close to the horse gets the player kicked.")]
        [SerializeField] float kickDistance = 6f;
        [SerializeField] float kickDamage = 10f;
        [SerializeField] float kickKnockback = 7f;
        [SerializeField] float kickCooldown = 3f;

        Interactable interactable;
        PlayerController player;
        bool spooked;
        float calmAt, nextWarningAt, nextKickAt;

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
                if (d.magnitude < kickDistance && Time.time >= nextKickAt) Kick(d);
            }
            else if (spooked && Time.time >= calmAt) spooked = false;

            interactable.enabled = !spooked;
        }

        // the horse lashes out at a runner: damage (it never kills), a throw back, a neigh
        void Kick(Vector3 fromHorse)
        {
            nextKickAt = Time.time + kickCooldown;
            Health health = player.Health;
            if (health == null || health.IsDead) return;

            float amount = Mathf.Min(kickDamage, health.Current - 1f);
            Vector3 away = fromHorse.sqrMagnitude > 0.01f ? fromHorse.normalized : -transform.forward;
            if (amount > 0f)
            {
                var info = new DamageInfo(amount, gameObject, player.transform.position + Vector3.up, false);
                if (health.TakeDamage(info)) player.GetComponentInParent<IHitReceiver>()?.OnHit(info, away * kickKnockback + Vector3.up * 2f);
                FloatingText.Spawn(player.transform.position + Vector3.up * 2.4f, "Ngựa đá!", new Color(1f, 0.45f, 0.25f));
            }
            player.Shake(0.5f);
            GameAudio.Play(Sfx.HorseNeigh, transform.position, 1f);
            GiftTracker.Announce("Ngựa hoảng sợ và đá bạn!\nĐừng chạy Shift khi lại gần nó");
        }

        void Ring()
        {
            if (HorseQuest.State != HorseQuestState.Cleared) return;
            QuestDialogue.Play(runner, rung, () => HorseQuest.Set(HorseQuestState.Calmed));
        }
    }
}
