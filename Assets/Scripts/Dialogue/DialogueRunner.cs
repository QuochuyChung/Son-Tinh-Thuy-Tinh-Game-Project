using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SonTinhThuyTinh.Dialogue
{
    // Shows a DialogueSequence line by line with a typewriter effect.
    // Space / Enter / E / click / gamepad A: finish the current line, or go to the next one. Esc / Start: skip the rest.
    public class DialogueRunner : MonoBehaviour
    {
        [SerializeField] CanvasGroup panel;
        [Tooltip("Hidden for narration lines (no speaker).")]
        [SerializeField] GameObject speakerPlate;
        [SerializeField] TMP_Text speakerText;
        [SerializeField] TMP_Text bodyText;
        [SerializeField] GameObject continueIndicator;
        [Tooltip("Optional. Shows DialogueLine.illustration full screen. With an Aspect Ratio Fitter (Envelope Parent) on the same object the picture covers the whole screen at any window shape, cropping the edges instead of leaving black bars.")]
        [SerializeField] Image illustration;
        [Tooltip("On: a line without an illustration keeps the previous one (Prologue). Off: it hides the picture, so a 3D cutscene shows through.")]
        [SerializeField] bool keepIllustration = true;
        [SerializeField] float charactersPerSecond = 40f;
        [SerializeField] bool allowSkip = true;

        InputAction advanceAction;
        InputAction skipAction;
        IReadOnlyList<DialogueLine> lines;
        Action onFinished;
        int lineIndex;
        int startedFrame;
        float revealed;
        bool typing;

        public bool IsPlaying => lines != null;
        public event Action<DialogueLine> LineStarted;

        void Awake()
        {
            advanceAction = new InputAction("Advance", InputActionType.Button);
            advanceAction.AddBinding("<Keyboard>/space");
            advanceAction.AddBinding("<Keyboard>/enter");
            // E opens a conversation with a villager (InteractionPrompt), so players press it again to go on: it advances too
            advanceAction.AddBinding("<Keyboard>/e");
            advanceAction.AddBinding("<Mouse>/leftButton");
            advanceAction.AddBinding("<Gamepad>/buttonSouth");

            skipAction = new InputAction("Skip", InputActionType.Button);
            skipAction.AddBinding("<Keyboard>/escape");
            skipAction.AddBinding("<Gamepad>/start");

            SetVisible(false);
        }

        void OnEnable()
        {
            advanceAction.Enable();
            skipAction.Enable();
        }

        void OnDisable()
        {
            advanceAction.Disable();
            skipAction.Disable();
        }

        void OnDestroy()
        {
            advanceAction.Dispose();
            skipAction.Dispose();
        }

        public void Play(DialogueSequence sequence, Action finished = null)
        {
            lines = sequence.Lines;
            onFinished = finished;
            lineIndex = -1;
            startedFrame = Time.frameCount;
            SetVisible(true);
            ShowNextLine();
        }

        void Update()
        {
            if (!IsPlaying) return;

            if (allowSkip && skipAction.WasPressedThisFrame())
            {
                Finish();
                return;
            }

            if (typing)
            {
                revealed += charactersPerSecond * Time.unscaledDeltaTime;
                bodyText.maxVisibleCharacters = Mathf.FloorToInt(revealed);
                if (revealed >= bodyText.textInfo.characterCount) CompleteLine();
            }

            // The press that started the dialogue must not also skip its first line.
            if (Time.frameCount == startedFrame || !advanceAction.WasPressedThisFrame()) return;

            if (typing) CompleteLine();
            else ShowNextLine();
        }

        void ShowNextLine()
        {
            lineIndex++;
            if (lineIndex >= lines.Count)
            {
                Finish();
                return;
            }

            DialogueLine line = lines[lineIndex];
            bool narration = string.IsNullOrEmpty(line.speaker);

            if (speakerPlate != null) speakerPlate.SetActive(!narration);
            speakerText.text = line.speaker;
            bodyText.text = line.text;
            bodyText.fontStyle = narration ? FontStyles.Italic : FontStyles.Normal;
            bodyText.maxVisibleCharacters = 0;
            bodyText.ForceMeshUpdate();

            if (illustration != null && line.illustration != null)
            {
                illustration.sprite = line.illustration;
                illustration.enabled = true;
                if (illustration.TryGetComponent(out AspectRatioFitter fitter))
                    fitter.aspectRatio = line.illustration.rect.width / line.illustration.rect.height;
            }
            else if (illustration != null && !keepIllustration)
            {
                illustration.enabled = false;
            }

            revealed = 0f;
            typing = true;
            if (continueIndicator != null) continueIndicator.SetActive(false);

            LineStarted?.Invoke(line);
        }

        void CompleteLine()
        {
            typing = false;
            bodyText.maxVisibleCharacters = bodyText.textInfo.characterCount;
            if (continueIndicator != null) continueIndicator.SetActive(true);
        }

        void Finish()
        {
            Action finished = onFinished;
            lines = null;
            onFinished = null;
            typing = false;
            SetVisible(false);
            finished?.Invoke();
        }

        void SetVisible(bool visible)
        {
            panel.alpha = visible ? 1f : 0f;
            panel.blocksRaycasts = visible;
        }
    }
}
