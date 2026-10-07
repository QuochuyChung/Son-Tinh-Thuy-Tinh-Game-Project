using System;
using System.Collections;
using SonTinhThuyTinh.Characters;
using SonTinhThuyTinh.Dialogue;
using SonTinhThuyTinh.Flow;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

namespace SonTinhThuyTinh.Cutscene
{
    // Hùng Vương's judgement in front of the palace (scene Cutscene_PhanXu, built by Tools ▸ Son Tinh Thuy Tinh ▸ Build Judgement Cutscene).
    // An intro Timeline flies the camera in over the courtyard, then the dialogue for the chosen character plays; every line cuts to its
    // camera and starts the gestures of the king and of Mị Nương (standing beside him). When the dialogue ends (or is skipped) the duel
    // scene loads.
    public class JudgementCutsceneDirector : MonoBehaviour
    {
        public enum Shot { Wide, King, KingLow, Player, Opponent, Suitors, MiNuong }
        public enum KingAction { Keep, Idle, Talk, Point, Nod }
        public enum MiNuongAction { Keep, Idle, Talk, Bow, Shy }

        [Serializable]
        public struct Cue
        {
            public Shot shot;
            public KingAction king;
            public MiNuongAction miNuong;
        }

        [Serializable]
        public class Script
        {
            public CharacterId player;
            public DialogueSequence dialogue;
            [Tooltip("One cue per dialogue line, same order.")]
            public Cue[] cues;
        }

        [SerializeField] DialogueRunner runner;
        [SerializeField] PlayableDirector intro;
        [SerializeField] CharacterRoster roster;
        [SerializeField] Script[] scripts;
        [Tooltip("Where each suitor stands. The model faces the mark's forward direction.")]
        [SerializeField] Transform sonTinhMark;
        [SerializeField] Transform thuyTinhMark;
        [SerializeField] Animator king;
        [Tooltip("Optional: without her model her lines show her illustration instead (DialogueLine.illustration).")]
        [SerializeField] Animator miNuong;
        [Tooltip("Cameras in the order of the Shot enum, except Player / Opponent: those use the Son Tinh / Thuy Tinh close-ups below.")]
        [SerializeField] CinemachineCamera wide;
        [SerializeField] CinemachineCamera kingCamera;
        [SerializeField] CinemachineCamera kingLow;
        [SerializeField] CinemachineCamera sonTinhClose;
        [SerializeField] CinemachineCamera thuyTinhClose;
        [SerializeField] CinemachineCamera suitors;
        [SerializeField] CinemachineCamera miNuongCamera;
        [SerializeField] string nextScene = SceneNames.Sandbox;
        [SerializeField] string nextScene = SceneNames.FinalBattle;
        [Tooltip("Used when the scene is played on its own, without character select.")]
        [SerializeField] CharacterId fallbackPlayer = CharacterId.SonTinh;

        CharacterId player;
        Script script;
        CinemachineCamera live;

        IEnumerator Start()
        {
            player = GameSession.SelectedCharacter ?? fallbackPlayer;
            script = Array.Find(scripts, s => s.player == player) ?? scripts[0];

            StandSuitor(CharacterId.SonTinh, sonTinhMark);
            StandSuitor(CharacterId.ThuyTinh, thuyTinhMark);

            if (intro != null && intro.playableAsset != null)
            {
                intro.Play();
                yield return null;   // the key that loaded the scene must not skip the intro
                while (intro.state == PlayState.Playing && intro.time < intro.duration - 0.05)
                {
                    if (SkipPressed()) { intro.time = intro.duration; intro.Evaluate(); intro.Stop(); break; }
                    yield return null;
                }
                yield return null;   // nor may the key that skipped it (Esc) skip the whole dialogue
            }

            runner.LineStarted += OnLine;
            runner.Play(script.dialogue, () => SceneLoader.Load(nextScene));
        }

        // Space / Enter / click / gamepad A or Esc / Start skip the fly-in, like they advance / skip the dialogue
        static bool SkipPressed()
        {
            var k = Keyboard.current; var m = Mouse.current; var g = Gamepad.current;
            return (k != null && (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame || k.escapeKey.wasPressedThisFrame))
                || (m != null && m.leftButton.wasPressedThisFrame)
                || (g != null && (g.buttonSouth.wasPressedThisFrame || g.startButton.wasPressedThisFrame));
        }

        void OnDestroy()
        {
            if (runner != null) runner.LineStarted -= OnLine;
        }

        void StandSuitor(CharacterId id, Transform mark)
        {
            CharacterDefinition character = roster.Get(id);
            if (character == null || mark == null) return;
            // only the visual part of the player prefab (mesh, Animator with its idle, weapon, outfit springs), no gameplay code
            Instantiate(character.PlayerPrefab.Animator.gameObject, mark, false);
        }

        void OnLine(DialogueLine line)
        {
            int index = IndexOf(line);
            if (index < 0 || script.cues == null || index >= script.cues.Length) return;
            Cue cue = script.cues[index];
            Cut(CameraFor(cue.shot));
            if (cue.king != KingAction.Keep && king != null) king.SetTrigger(cue.king.ToString());
            if (cue.miNuong != MiNuongAction.Keep && miNuong != null) miNuong.SetTrigger(cue.miNuong.ToString());
        }

        int IndexOf(DialogueLine line)
        {
            var lines = script.dialogue.Lines;
            for (int i = 0; i < lines.Count; i++)
                if (ReferenceEquals(lines[i], line)) return i;
            return -1;
        }

        CinemachineCamera CameraFor(Shot shot)
        {
            bool sonTinhIsPlayer = player == CharacterId.SonTinh;
            switch (shot)
            {
                case Shot.King: return kingCamera;
                case Shot.KingLow: return kingLow;
                case Shot.Player: return sonTinhIsPlayer ? sonTinhClose : thuyTinhClose;
                case Shot.Opponent: return sonTinhIsPlayer ? thuyTinhClose : sonTinhClose;
                case Shot.Suitors: return suitors;
                case Shot.MiNuong: return miNuongCamera != null ? miNuongCamera : kingCamera;
                default: return wide;
            }
        }

        void Cut(CinemachineCamera next)
        {
            if (next == null || next == live) return;
            if (live != null) live.Priority = 0;
            next.Priority = 100;
            live = next;
        }
    }
}
