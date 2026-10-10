using System;
using System.Collections;
using System.Collections.Generic;
using SonTinhThuyTinh.CameraSystem;
using SonTinhThuyTinh.Combat;
using SonTinhThuyTinh.Dialogue;
using SonTinhThuyTinh.Player;
using Unity.Cinemachine;
using UnityEngine;

namespace SonTinhThuyTinh.Quest
{
    public enum RoosterQuestState
    {
        NotStarted,   // the rooster pecks in the stone ring; the thieves have not come yet
        Fighting,     // the cutscene was seen: two Ninja fight, the leader holds the rooster
        RoosterFree,  // both Ninja beaten: the leader dropped the rooster and ran, catch it (E)
        Completed,    // caught: Gà Chín Cựa is in the gift list
    }

    // Static like HorseQuest: leaving the map and coming back keeps the progress (no second cutscene, no second rooster).
    public static class RoosterQuest
    {
        public static RoosterQuestState State { get; private set; }
        public static event Action<RoosterQuestState> Changed;

        public static void Set(RoosterQuestState state)
        {
            if (state == State) return;
            State = state;
            Changed?.Invoke(state);
        }

        public static void Reset() => State = RoosterQuestState.NotStarted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() { State = RoosterQuestState.NotStarted; Changed = null; }
    }

    // Gà Chín Cựa on Map_SonTinh (docs/ke-hoach-ga-chin-cua.md, built by Assets/Editor/RoosterQuestBuilder.cs):
    //  1. Sơn Tinh comes near the stone ring -> 3D cutscene with dialogue: the time keeps running (the actors act during the lines), the
    //     player's controls are off, each line cuts to its camera shot and starts its action (the Ninja run in, the leader grabs the bird,
    //     Sơn Tinh steps in, the leader backs off, two Ninja draw their swords).
    //  2. Fight: two NinjaEnemy, one attacking at a time (this hands out the turns), the leader stands at the edge holding the rooster.
    //     Sơn Tinh knocked out -> back at the edge of the ring, both Ninja full health, the fight starts again (no cutscene).
    //  3. Both Ninja dead -> the leader shouts, drops the rooster and runs off; the rooster runs about the ring (RoosterRunner), E catches it.
    // From the start of the fight until the rooster is caught the player cannot leave the ring (ArenaBoundary).
    public class RoosterQuestDirector : MonoBehaviour
    {
        [Header("Cast")]
        [SerializeField] NinjaEnemy[] fighters;
        [SerializeField] NinjaEnemy leader;
        [SerializeField] RoosterRunner rooster;
        [SerializeField] Transform leaderChest;     // where the rooster sits in the leader's arms

        [Header("Places")]
        [SerializeField] Transform arenaCenter;
        [SerializeField] float arenaRadius = 9.5f;
        [Tooltip("The cutscene starts when the player comes this close to the ring's centre.")]
        [SerializeField] float triggerRadius = 20f;
        [SerializeField] Transform[] fighterEntries;   // outside the ring: where the Ninja come running from
        [SerializeField] Transform[] fighterSpots;     // round the rooster, then where the fight starts
        [SerializeField] Transform leaderEntry, leaderGrabSpot, leaderEdge, leaderExit;
        [SerializeField] Transform playerMark;         // where Sơn Tinh steps in (and comes back after a loss)
        [Tooltip("Keeps the player in the ring from the start of the fight until the rooster is caught.")]
        [SerializeField] ArenaBoundary boundary;

        [Header("Cutscene")]
        [SerializeField] DialogueRunner runner;
        [SerializeField] DialogueSequence intro;
        [SerializeField] DialogueSequence flee;
        [SerializeField] CinemachineCamera cutsceneCamera;
        [Tooltip("One per intro line: the camera cuts there when the line starts (empty = keep the shot).")]
        [SerializeField] Transform[] shots;
        [SerializeField] GiftItem gift;

        PlayerController player;
        NinjaEnemy turn;
        float turnFreeAt;
        bool running, fightOn, resetting;
        int line;
        readonly List<Coroutine> actions = new();

        void Start()
        {
            foreach (var n in fighters) { n.Setup(arenaCenter, arenaRadius, RequestTurn, EndTurn); n.Killed += OnKilled; }
            leader.Setup(arenaCenter, arenaRadius + 3f, null, null);
            leader.Health.IsInvulnerable = true;   // he only holds the rooster
            if (cutsceneCamera != null) cutsceneCamera.gameObject.SetActive(false);
            if (gift != null && GiftTracker.Has(gift)) RoosterQuest.Set(RoosterQuestState.Completed);
            // a quest state left from an earlier game whose gifts were cleared (back to the main menu, new game): start over
            else if (gift != null && RoosterQuest.State == RoosterQuestState.Completed) RoosterQuest.Reset();

            switch (RoosterQuest.State)
            {
                case RoosterQuestState.NotStarted:
                    foreach (var n in fighters) n.gameObject.SetActive(false);
                    leader.gameObject.SetActive(false);
                    rooster.transform.position = arenaCenter.position;
                    rooster.SetIdle("Eat");
                    break;
                case RoosterQuestState.Fighting:
                    PlaceForFight();
                    break;
                case RoosterQuestState.RoosterFree:
                    foreach (var n in fighters) n.gameObject.SetActive(false);
                    leader.gameObject.SetActive(false);
                    rooster.transform.position = arenaCenter.position;
                    rooster.Release(transform);
                    break;
                default:
                    foreach (var n in fighters) n.gameObject.SetActive(false);
                    leader.gameObject.SetActive(false);
                    rooster.gameObject.SetActive(false);
                    break;
            }
            rooster.Caught += () => { RoosterQuest.Set(RoosterQuestState.Completed); if (boundary != null) boundary.Release(); };
            if (RoosterQuest.State == RoosterQuestState.RoosterFree && boundary != null) boundary.Engage();
        }

        void Update()
        {
            FindPlayer();
            if (player == null || running) return;
            float d = Flat(player.transform.position - arenaCenter.position).magnitude;

            if (RoosterQuest.State == RoosterQuestState.NotStarted && d < triggerRadius) StartCoroutine(Cutscene());
            else if (RoosterQuest.State == RoosterQuestState.Fighting)
            {
                if (!fightOn && d < arenaRadius + 2f) StartCoroutine(StartFight());
                else if (fightOn && player.Health.IsDead && !resetting) StartCoroutine(ResetAfterDefeat());
                else if (fightOn && d > triggerRadius + 15f) { foreach (var n in fighters) if (!n.IsDead) n.StopFight(); PlaceForFight(); }   // ran away
            }
        }

        // ---------------------------------------------------------------- cutscene

        IEnumerator Cutscene()
        {
            running = true;
            SetPlayerControl(false);
            if (cutsceneCamera != null) cutsceneCamera.gameObject.SetActive(true);
            line = -1;
            bool done = false;
            runner.LineStarted += OnLine;
            runner.Play(intro, () => done = true);
            while (!done) yield return null;
            runner.LineStarted -= OnLine;

            // whatever the lines did not get to (skipped with Esc, or pressed through quickly): put everyone in place
            foreach (var c in actions) if (c != null) StopCoroutine(c);
            actions.Clear();
            for (int i = line + 1; i < intro.Lines.Count; i++) Beat(i, true);
            // everyone on their marks (not a reset: a Ninja that already drew its sword keeps it)
            for (int k = 0; k < fighters.Length; k++)
                fighters[k].transform.SetPositionAndRotation(fighterSpots[k].position, fighterSpots[k].rotation);
            leader.transform.SetPositionAndRotation(leaderEdge.position, leaderEdge.rotation);
            leader.Act("Hold");
            leader.SetHolding(true);
            if (rooster.Current != RoosterRunner.Mode.Held) rooster.Hold(leaderChest);
            if (cutsceneCamera != null) cutsceneCamera.gameObject.SetActive(false);
            SetPlayerControl(true);
            RoosterQuest.Set(RoosterQuestState.Fighting);
            yield return StartFight();
            running = false;
        }

        void OnLine(DialogueLine _)
        {
            line++;
            if (shots != null && line < shots.Length && shots[line] != null && cutsceneCamera != null)
                cutsceneCamera.transform.SetPositionAndRotation(shots[line].position, shots[line].rotation);
            Beat(line, false);
        }

        // what happens on each intro line (Dialogue_RoosterQuest_Intro); instant = only the end result (skipped lines)
        void Beat(int i, bool instant)
        {
            switch (i)
            {
                case 1:   // the thieves run in from the trees
                    for (int k = 0; k < fighters.Length; k++)
                    {
                        var n = fighters[k];
                        n.gameObject.SetActive(true);
                        n.ResetTo(instant ? fighterSpots[k].position : fighterEntries[k].position, fighterSpots[k].rotation);
                        if (!instant) actions.Add(StartCoroutine(n.RunTo(fighterSpots[k].position, 5f)));
                    }
                    leader.gameObject.SetActive(true);
                    leader.ResetTo(instant ? leaderGrabSpot.position : leaderEntry.position, leaderGrabSpot.rotation);
                    if (!instant) actions.Add(StartCoroutine(leader.RunTo(leaderGrabSpot.position, 5f)));
                    break;
                case 2:   // the leader grabs the rooster
                    leader.transform.SetPositionAndRotation(leaderGrabSpot.position, leaderGrabSpot.rotation);
                    leader.Face(arenaCenter.position);
                    leader.Act("Hold");
                    leader.SetHolding(true);
                    rooster.Hold(leaderChest);
                    break;
                case 3:   // Sơn Tinh steps in
                    if (player != null)
                    {
                        Teleport(playerMark);
                        foreach (var n in fighters) n.Face(player.transform.position);
                        leader.Face(player.transform.position);
                    }
                    break;
                case 5:   // the leader backs off to the edge with the bird
                    if (instant) leader.transform.SetPositionAndRotation(leaderEdge.position, leaderEdge.rotation);
                    else actions.Add(StartCoroutine(BackOff()));
                    break;
                case 6:   // two Ninja draw their swords
                    foreach (var n in fighters) if (!instant) actions.Add(StartCoroutine(n.DrawSword()));
                    break;
            }
        }

        IEnumerator BackOff()
        {
            yield return leader.RunTo(leaderEdge.position, 3.6f);   // a jog at nearly the clip's own pace (slower looked like slow motion), arms around the bird (HoldArms layer)
            if (player != null) leader.Face(player.transform.position);
            leader.Act("Hold");
        }

        // ---------------------------------------------------------------- fight

        void PlaceForFight(bool keepPlayer = false)
        {
            fightOn = false;
            for (int k = 0; k < fighters.Length; k++)
            {
                fighters[k].gameObject.SetActive(true);
                fighters[k].ResetTo(fighterSpots[k].position, fighterSpots[k].rotation);
            }
            leader.gameObject.SetActive(true);
            leader.ResetTo(leaderEdge.position, leaderEdge.rotation);
            leader.Act("Hold");
            leader.SetHolding(true);
            if (rooster.Current != RoosterRunner.Mode.Held) rooster.Hold(leaderChest);
            turn = null;
        }

        IEnumerator StartFight()
        {
            running = true;
            if (player != null) foreach (var n in fighters) n.Face(player.transform.position);
            // a Ninja whose draw was cut short (lines pressed through quickly) or that was reset draws now: none fights empty-handed
            bool drawing = false;
            foreach (var n in fighters) if (!n.IsArmed) { StartCoroutine(n.DrawSword()); drawing = true; }
            if (drawing) yield return new WaitForSeconds(0.6f);
            foreach (var n in fighters) n.BeginFight();
            fightOn = true;
            if (boundary != null) boundary.Engage();
            running = false;
        }

        bool RequestTurn(NinjaEnemy n)
        {
            if (turn != null && turn != n && !turn.IsDead) return false;
            if (turn != n && Time.time < turnFreeAt) return false;
            turn = n;
            return true;
        }

        void EndTurn(NinjaEnemy n)
        {
            if (turn != n) return;
            turn = null;
            turnFreeAt = Time.time + 0.7f;   // the other one waits a moment: no blows from both sides at once
        }

        void OnKilled(NinjaEnemy _)
        {
            foreach (var n in fighters) if (!n.IsDead) return;
            if (!fightOn) return;
            fightOn = false;
            StartCoroutine(LeaderFlees());
        }

        IEnumerator LeaderFlees()
        {
            running = true;
            yield return new WaitForSeconds(1.2f);
            if (player != null) leader.Face(player.transform.position);
            bool done = false;
            QuestDialogue.Play(runner, flee, () => done = true);
            while (!done) yield return null;
            rooster.Release(transform);                       // dropped: it runs about the ring
            leader.SetHolding(false);
            RoosterQuest.Set(RoosterQuestState.RoosterFree);
            GiftTracker.Announce("Gà chín cựa chạy mất!\nĐến gần và nhấn <b>E</b> để bắt");
            Vector3 exit = leaderExit.position;
            yield return leader.RunTo(exit, 6f);
            leader.gameObject.SetActive(false);
            running = false;
        }

        IEnumerator ResetAfterDefeat()
        {
            resetting = true; running = true;
            if (boundary != null) boundary.Release();
            foreach (var n in fighters) if (!n.IsDead) n.StopFight();
            yield return new WaitForSeconds(3f);
            PlaceForFight();
            Teleport(playerMark);
            player.Revive();
            yield return new WaitForSeconds(1f);
            resetting = false; running = false;   // the fight starts again when the player steps into the ring (Update)
        }

        // ---------------------------------------------------------------- player

        void FindPlayer()
        {
            if (player != null && player.isActiveAndEnabled) return;
            player = null;
            foreach (PlayerController c in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
                if (c.isActiveAndEnabled) { player = c; break; }
        }

        void SetPlayerControl(bool on)
        {
            if (player == null) return;
            if (!on) player.SetSpeed(0f);
            player.InputReader.enabled = on;
            if (!on) player.InputReader.ClearBuffered();
            var cam = FindAnyObjectByType<ThirdPersonCameraInput>();
            if (cam != null) cam.enabled = on;
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
