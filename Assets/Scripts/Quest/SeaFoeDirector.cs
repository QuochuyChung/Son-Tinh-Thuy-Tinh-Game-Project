using System.Collections;
using System.Collections.Generic;
using SonTinhThuyTinh.Combat;
using SonTinhThuyTinh.Dialogue;
using SonTinhThuyTinh.Player;
using UnityEngine;

namespace SonTinhThuyTinh.Quest
{
    // A band of ordinary fighters on Map_ThuyTinh (NinjaEnemy, the same ones as the horse thieves of Map_SonTinh): the pirates on the
    // harbour deck or the water ghosts at the foot of the lighthouse (docs/ke-hoach-thuy-tinh-su-kien.md). Like HorseQuestDirector:
    // they stand in their places; when the player comes close they speak, draw their swords and fight, at most two strike at once;
    // knocked out -> back at the mark, the band at full health, fight again when the player returns; all down -> the quest moves on.
    //  - Pirates: present until the harbour is cleared (SeaQuestState.Cleared); they only fight once the fisherman's errand is
    //    accepted. `blockers` (the chain across the pier) stand as long as they are present. Beaten -> SeaQuest.Cleared.
    //  - Ghosts: appear once the harbour is cleared, until the lamp's guards are beaten (SeaQuest.GhostsCleared).
    public class SeaFoeDirector : MonoBehaviour
    {
        public enum Kind { Pirates, Ghosts }

        [SerializeField] Kind kind;
        [SerializeField] NinjaEnemy[] foes;
        [SerializeField] Transform[] spots;
        [SerializeField] Transform centre;
        [SerializeField] float radius = 10f;
        [Tooltip("They speak and attack when the player comes this close to the centre.")]
        [SerializeField] float triggerRadius = 16f;
        [Tooltip("The player comes back here after a defeat.")]
        [SerializeField] Transform playerMark;
        [Tooltip("Stand while the band is present (a chain across the pier).")]
        [SerializeField] GameObject[] blockers;
        [SerializeField] DialogueRunner runner;
        [SerializeField] DialogueSequence ambush;

        PlayerController player;
        bool fightOn, running, introSeen, present, clearedOnce;
        float turnFreeAt;
        readonly List<NinjaEnemy> attackers = new();

        bool Present => kind == Kind.Pirates ? SeaQuest.State != SeaQuestState.Cleared : SeaQuest.State == SeaQuestState.Cleared && !SeaQuest.GhostsCleared;
        bool Fightable => kind == Kind.Pirates ? SeaQuest.State == SeaQuestState.Accepted : Present;

        void Start()
        {
            foreach (var f in foes) { f.Setup(centre, radius, RequestTurn, EndTurn); f.Killed += OnKilled; }
            SeaQuest.Changed += Apply;
            Apply();
        }

        void OnDestroy() => SeaQuest.Changed -= Apply;

        void Apply()
        {
            bool now = Present;
            if (now == present && present) return;   // still here: do not reset a fight under way
            present = now;
            StopAllCoroutines();
            fightOn = false; running = false; clearedOnce = false; attackers.Clear();
            for (int i = 0; i < foes.Length; i++)
            {
                foes[i].gameObject.SetActive(present);
                if (present) foes[i].ResetTo(spots[i].position, spots[i].rotation);
            }
            foreach (var b in blockers) if (b != null) b.SetActive(present);
        }

        void Update()
        {
            if (!present || !Fightable) return;
            FindPlayer();
            if (player == null || running) return;
            float d = Flat(player.transform.position - centre.position).magnitude;

            if (!fightOn)
            {
                if (d < triggerRadius && !player.Health.IsDead) StartCoroutine(StartFight());
            }
            else if (player.Health.IsDead) StartCoroutine(ResetAfterDefeat());
            else if (d > triggerRadius + 20f)   // ran away: they go back to their places
            {
                foreach (var f in foes) if (!f.IsDead) f.StopFight();
                PlaceFoes();
            }
        }

        IEnumerator StartFight()
        {
            running = true;
            foreach (var f in foes) if (!f.IsDead && player != null) f.Face(player.transform.position);
            if (!introSeen && ambush != null)
            {
                introSeen = true;
                bool done = false;
                QuestDialogue.Play(runner, ambush, () => done = true);
                while (!done) yield return null;
            }
            bool drawing = false;
            foreach (var f in foes) if (!f.IsDead && !f.IsArmed) { StartCoroutine(f.DrawSword()); drawing = true; }
            if (drawing) yield return new WaitForSeconds(0.7f);
            foreach (var f in foes) if (!f.IsDead) f.BeginFight();
            fightOn = true;
            running = false;
        }

        // at most two strike at once while most of them are up, one at a time after that
        bool RequestTurn(NinjaEnemy n)
        {
            if (attackers.Contains(n)) return true;
            int alive = 0;
            foreach (var f in foes) if (!f.IsDead) alive++;
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
            foreach (var f in foes) if (!f.IsDead) return;
            if (clearedOnce) return;   // (also when they fall to a spell before the fight started)
            clearedOnce = true;
            fightOn = false;
            StartCoroutine(Cleared());
        }

        IEnumerator Cleared()
        {
            running = true;
            yield return new WaitForSeconds(2.5f);   // the last one falls first
            if (kind == Kind.Pirates)
            {
                GiftTracker.Announce("Bọn ngư tặc đã bị đánh bại!\nDây xích chắn bến đã được gỡ, sang Làng Chài gặp trưởng làng");
                SeaQuest.Set(SeaQuestState.Cleared);
            }
            else
            {
                GiftTracker.Announce("Ma nước đã tan!\nLên chân tháp thắp lại ngọn đèn hải đăng (E)");
                SeaQuest.ClearGhosts();
            }
            running = false;
        }

        IEnumerator ResetAfterDefeat()
        {
            running = true;
            foreach (var f in foes) if (!f.IsDead) f.StopFight();
            yield return new WaitForSeconds(3f);
            PlaceFoes();
            Teleport(playerMark);
            player.Revive();
            yield return new WaitForSeconds(1f);
            running = false;   // the fight starts again when the player comes back (Update)
        }

        void PlaceFoes()
        {
            fightOn = false; attackers.Clear();
            for (int i = 0; i < foes.Length; i++)
            {
                foes[i].gameObject.SetActive(true);
                foes[i].ResetTo(spots[i].position, spots[i].rotation);
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
