using System;
using System.Collections;
using SonTinhThuyTinh.Characters;
using SonTinhThuyTinh.Dialogue;
using SonTinhThuyTinh.Flow;
using SonTinhThuyTinh.UI;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.UI;

namespace SonTinhThuyTinh.Cutscene
{
    // The ending in front of the palace (scene Cutscene_Ending, built by Tools ▸ Son Tinh Thuy Tinh ▸ Build Ending Cutscene).
    // An intro Timeline flies the camera in, then Hùng Vương judges the duel that just happened in Map_FinalBattle: the winner and the
    // loser BOTH stand and listen (neither kneels), the king speaks, Mị Nương answers. Two dialogues, one per winning character.
    // Afterwards an epilogue card appears; Space / Esc returns to the MainMenu with a fresh session.
    public class EndingCutsceneDirector : MonoBehaviour
    {
        public enum Shot { Wide, King, KingLow, MiNuong, Winner, Loser, Couple }
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
            [Tooltip("The character that WON the duel; their dialogue plays.")]
            public CharacterId player;
            public DialogueSequence dialogue;
            [Tooltip("One cue per dialogue line, same order.")]
            public Cue[] cues;
        }

        [SerializeField] DialogueRunner runner;
        [SerializeField] PlayableDirector intro;
        [SerializeField] CharacterRoster roster;
        [SerializeField] Script[] scripts;
        [Tooltip("Where the winner stands (left); the loser uses the right mark.")]
        [SerializeField] Transform winnerMark;
        [SerializeField] Transform loserMark;
        [SerializeField] Animator king;
        [SerializeField] Animator miNuong;
        [SerializeField] CinemachineCamera wide;
        [SerializeField] CinemachineCamera kingCamera;
        [SerializeField] CinemachineCamera kingLow;
        [SerializeField] CinemachineCamera winnerClose;
        [SerializeField] CinemachineCamera loserClose;
        [SerializeField] CinemachineCamera couple;
        [SerializeField] CinemachineCamera miNuongCamera;
        [SerializeField] string nextScene = SceneNames.MainMenu;
        [Tooltip("Used when the scene is played on its own, without the duel.")]
        [SerializeField] CharacterId fallbackPlayer = CharacterId.SonTinh;

        CharacterId winner;
        CharacterId loser;
        Script script;
        CinemachineCamera live;
        bool epilogueShown;

        IEnumerator Start()
        {
            winner = GameSession.SelectedCharacter ?? fallbackPlayer;
            loser = winner == CharacterId.SonTinh ? CharacterId.ThuyTinh : CharacterId.SonTinh;
            script = Array.Find(scripts, s => s.player == winner) ?? scripts[0];

            StandSuitor(winner, winnerMark);
            StandSuitor(loser, loserMark);

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
            runner.Play(script.dialogue, ShowEpilogue);
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
            switch (shot)
            {
                case Shot.King: return kingCamera;
                case Shot.KingLow: return kingLow;
                case Shot.MiNuong: return miNuongCamera != null ? miNuongCamera : kingCamera;
                case Shot.Winner: return winnerClose;
                case Shot.Loser: return loserClose;
                case Shot.Couple: return couple != null ? couple : wide;
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

        // ---------------------------------------------------------------- epilogue

        // Built at runtime: the scene only needs this component and the dialogue UI.
        void ShowEpilogue()
        {
            if (epilogueShown) return;
            epilogueShown = true;

            var canvasGo = new GameObject("EpilogueCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var group = canvasGo.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            var dim = new GameObject("Dim", typeof(RectTransform), typeof(Image));
            dim.transform.SetParent(canvasGo.transform, false);
            var dimRt = (RectTransform)dim.transform;
            dimRt.anchorMin = Vector2.zero; dimRt.anchorMax = Vector2.one;
            dimRt.offsetMin = dimRt.offsetMax = Vector2.zero;
            dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.88f);

            string title = winner == CharacterId.SonTinh ? "Sơn Tinh" : "Thủy Tinh";
            string epilogue = winner == CharacterId.SonTinh
                ? "Sơn Tinh rước Mị Nương về núi Tản Viên.\nNúi cao che sóng, muôn dân sống yên."
                : "Thủy Tinh rước Mị Nương về chốn biển khơi.\nNước lành chảy khắp đồng, muôn dân sống đủ.";

            CreateText(canvasGo.transform, "Title", title, 80f, FontStyles.Bold, new Vector2(0f, 140f), new Vector2(1200f, 100f));
            CreateText(canvasGo.transform, "Body", epilogue, 34f, FontStyles.Italic, new Vector2(0f, 20f), new Vector2(1400f, 160f));
            CreateText(canvasGo.transform, "Prompt", "Nhấn Space để về Menu", 26f, FontStyles.Normal, new Vector2(0f, -140f), new Vector2(800f, 40f));

            StartCoroutine(EpilogueRoutine(group));
        }

        IEnumerator EpilogueRoutine(CanvasGroup group)
        {
            yield return CanvasGroupFade.Run(group, 0f, 1f, 0.8f);
            yield return null;   // the key that finished the dialogue must not dismiss the card

            while (!SkipPressed()) yield return null;

            GameSession.StartNewGame();
            SceneLoader.Load(nextScene);
        }

        static void CreateText(Transform parent, string name, string content, float size, FontStyles style, Vector2 pos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;
            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
        }
    }
}
