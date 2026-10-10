using System.Collections;
using System.Collections.Generic;
using SonTinhThuyTinh.Combat;
using SonTinhThuyTinh.Dialogue;
using SonTinhThuyTinh.Player;
using UnityEngine;

namespace SonTinhThuyTinh.Quest
{
    // The mountain part of the horse errand on Map_SonTinh (built by Assets/Editor/HorseQuestBuilder.cs, trail: SonTinhMountainBranch):
    //  - Accepted: three horse thieves wait in the clearing (NinjaEnemy, the same fighters as the rooster thieves) and a wooden barricade
    //    shuts the trail beyond it. When the player comes close they speak, draw their swords and fight: at most two strike at the same
    //    time. Knocked out -> back at the checkpoint before the clearing, thieves full health, fight again when the player returns.
    //  - All three down -> the barricade falls (Cleared), the way to the horse on the summit is open (HorseSummit).
    //  - Calmed -> the horse is back in the stable (this switches the stable horse on); Completed -> nobody is here any more.
    // Everything is switched by HorseQuest.State, also when the map is loaded again.
    public class HorseQuestDirector : MonoBehaviour
    {
        [Header("Thieves")]
        [SerializeField] NinjaEnemy[] thieves;
        [SerializeField] Transform[] spots;
        [SerializeField] Transform clearingCenter;
        [SerializeField] float clearingRadius = 12f;
        [Tooltip("The thieves speak and attack when the player comes this close to the clearing's centre.")]
        [SerializeField] float triggerRadius = 17f;
        [Tooltip("The player comes back here after a defeat.")]
        [SerializeField] Transform playerMark;
        [SerializeField] GameObject barricade;

        [Header("Horses")]
        [SerializeField] GameObject summitHorse;
        [SerializeField] GameObject stableHorse;

        [Header("Dialogue")]
        [SerializeField] DialogueRunner runner;
        [SerializeField] DialogueSequence ambush;

        PlayerController player;
        bool fightOn, running, introSeen;
        float turnFreeAt;
        readonly List<NinjaEnemy> attackers = new();

        void Start()
        {
            foreach (var t in thieves) { t.Setup(clearingCenter, clearingRadius, RequestTurn, EndTurn); t.Killed += OnKilled; }
            HorseQuest.Changed += Apply;
            Apply(HorseQuest.State);
        }

        void OnDestroy() => HorseQuest.Changed -= Apply;

        // what is in the scene in each state
        void Apply(HorseQuestState state)
        {
            bool accepted = state == HorseQuestState.Accepted;
            StopAllCoroutines();
            fightOn = false; running = false; attackers.Clear();
            for (int i = 0; i < thieves.Length; i++)
            {
                if (accepted)
                {
                    thieves[i].gameObject.SetActive(true);
                    thieves[i].ResetTo(spots[i].position, spots[i].rotation);
                }
                else thieves[i].gameObject.SetActive(false);
            }
            if (barricade != null) barricade.SetActive(accepted);
            if (summitHorse != null) summitHorse.SetActive(state == HorseQuestState.Accepted || state == HorseQuestState.Cleared);
            if (stableHorse != null) stableHorse.SetActive(state == HorseQuestState.Calmed);
        }

        void Update()
        {
            if (HorseQuest.State != HorseQuestState.Accepted) return;
            FindPlayer();
            if (player == null || running) return;
            float d = Flat(player.transform.position - clearingCenter.position).magnitude;

            if (!fightOn)
            {
                if (d < triggerRadius && !player.Health.IsDead) StartCoroutine(StartFight());
            }
            else if (player.Health.IsDead) StartCoroutine(ResetAfterDefeat());
            else if (d > triggerRadius + 20f)   // ran away: the thieves go back to their places
            {
                foreach (var t in thieves) if (!t.IsDead) t.StopFight();
                PlaceThieves();
            }
        }

        IEnumerator StartFight()
        {
            running = true;
            foreach (var t in thieves) if (!t.IsDead && player != null) t.Face(player.transform.position);
            if (!introSeen && ambush != null)
            {
                introSeen = true;
                bool done = false;
                QuestDialogue.Play(runner, ambush, () => done = true);
                while (!done) yield return null;
            }
            bool drawing = false;
            foreach (var t in thieves) if (!t.IsDead && !t.IsArmed) { StartCoroutine(t.DrawSword()); drawing = true; }
            if (drawing) yield return new WaitForSeconds(0.7f);
            foreach (var t in thieves) if (!t.IsDead) t.BeginFight();
            fightOn = true;
            running = false;
        }

        // at most two strike at once while all three are up, one at a time after that
        bool RequestTurn(NinjaEnemy n)
        {
            if (attackers.Contains(n)) return true;
            int alive = 0;
            foreach (var t in thieves) if (!t.IsDead) alive++;
            if (attackers.Count >= (alive >= 3 ? 2 : 1) || Time.time < turnFreeAt) return false;
            attackers.Add(n);
            return true;
        }

        void EndTurn(NinjaEnemy n)
        {
            if (!attackers.Remove(n)) return;
            turnFreeAt = Time.time + 0.45f;
        }

        void OnKilled(NinjaEnemy _)
        {
            foreach (var t in thieves) if (!t.IsDead) return;
            if (!fightOn) return;
            fightOn = false;
            StartCoroutine(Cleared());
        }

        IEnumerator Cleared()
        {
            running = true;
            yield return new WaitForSeconds(2.5f);   // the last one falls first
            GiftTracker.Announce("Bọn trộm ngựa đã bị đánh bại!\nHàng rào chắn đường đã đổ, lên đỉnh gò tìm ngựa");
            HorseQuest.Set(HorseQuestState.Cleared);
            running = false;
        }

        IEnumerator ResetAfterDefeat()
        {
            running = true;
            foreach (var t in thieves) if (!t.IsDead) t.StopFight();
            yield return new WaitForSeconds(3f);
            PlaceThieves();
            Teleport(playerMark);
            player.Revive();
            yield return new WaitForSeconds(1f);
            running = false;   // the fight starts again when the player comes back to the clearing (Update)
        }

        void PlaceThieves()
        {
            fightOn = false; attackers.Clear();
            for (int i = 0; i < thieves.Length; i++)
            {
                thieves[i].gameObject.SetActive(true);
                thieves[i].ResetTo(spots[i].position, spots[i].rotation);
            }
        }

        void FindPlayer()
        {
            if (player != null && player.isActiveAndEnabled) return;
            player = null;
            foreach (PlayerController c in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
                if (c.isActiveAndEnabled) { player = c; break; }
        }

        void Teleport(Transform mark)
        {
            if (player == null || mark == null) return;
            player.SetBodyEnabled(false);
            player.transform.SetPositionAndRotation(mark.position, mark.rotation);
            player.SetBodyEnabled(true);
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }
}
