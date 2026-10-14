using System;
using System.Collections;
using SonTinhThuyTinh.Dialogue;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SonTinhThuyTinh.Cutscene
{
    // The base of the 3D cutscenes (opening, judgement, ending). A cutscene is a coroutine, Run(), written as a script:
    //     yield return Chapter(Title());                  // a stretch without dialogue; Space / Enter / Esc / click skips it
    //     yield return Dialogue(sequence, Beat);          // the dialogue box; Beat(i) is what the camera and the actors do on line i
    // Beat(i) returns a coroutine that starts when line i appears and is cut short when the next line starts, so a line can hold
    // several shots in a row (the reader's pace decides how many of them are seen). Slow motion, shakes and the rest come from the
    // CinematicCamera, CinematicFx and CineStage.
    public abstract class CinematicDirector : MonoBehaviour
    {
        [SerializeField] protected DialogueRunner runner;
        [SerializeField] protected CinematicCamera cam;
        [SerializeField] protected CineStage stage;
        [SerializeField] protected Camera outputCamera;

        protected CinematicFx fx;
        bool skip;
        int skipFrame = -1;
        Coroutine beat;
        readonly System.Collections.Generic.List<Coroutine> loose = new();
        Func<int, IEnumerator> beats;
        DialogueSequence current;

        protected abstract IEnumerator Run();

        protected virtual IEnumerator Start()
        {
            Application.runInBackground = true;
            Time.timeScale = 1f;
            var font = runner != null ? runner.Font : null;
            int order = runner != null && runner.PanelCanvas != null ? runner.PanelCanvas.sortingOrder : 0;
            fx = CinematicFx.Create(font, order);
            fx.FadeNow(1f);
            cam.Activate(true);
            yield return null;   // the key that loaded the scene must not skip the first chapter
            yield return Run();
        }

        protected virtual void OnDestroy()
        {
            if (runner != null) runner.LineStarted -= OnLine;
            Time.timeScale = 1f;
        }

        void Update()
        {
            var k = Keyboard.current; var m = Mouse.current; var g = Gamepad.current;
            bool pressed = (k != null && (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame || k.escapeKey.wasPressedThisFrame))
                        || (m != null && m.leftButton.wasPressedThisFrame)
                        || (g != null && (g.buttonSouth.wasPressedThisFrame || g.startButton.wasPressedThisFrame));
            if (pressed && skipFrame != Time.frameCount) skip = true;
        }

        // ---------------------------------------------------------------- the script's tools

        // A stretch without dialogue: runs until it ends or the player skips it (then it is abandoned where it stands).
        protected IEnumerator Chapter(IEnumerator body)
        {
            skip = false;
            bool done = false;
            var co = StartCoroutine(Wrapped(body, () => done = true));
            while (!done && !skip) yield return null;
            if (!done) StopCoroutine(co);
            StopLoose();
            fx.HideAll();
            skip = false;
            skipFrame = Time.frameCount;
            Time.timeScale = 1f;
        }

        static IEnumerator Wrapped(IEnumerator body, Action finished)
        {
            yield return body;
            finished();
        }

        // Seconds of unscaled time, cut short by a skip (Chapter drops the rest anyway).
        protected static WaitForSecondsRealtime Wait(float seconds) => new(seconds);

        // Two things at once: starts the second and runs the first.
        protected IEnumerator Also(IEnumerator main, IEnumerator side)
        {
            Go(side);
            yield return main;
        }

        // Runs the dialogue box; beat(i) is started on line i and stopped when line i + 1 starts. Returns when the dialogue ends.
        protected IEnumerator Dialogue(DialogueSequence sequence, Func<int, IEnumerator> beat)
        {
            current = sequence; beats = beat;
            bool done = false;
            runner.LineStarted += OnLine;
            runner.Play(sequence, () => done = true);
            while (!done) yield return null;
            runner.LineStarted -= OnLine;
            if (this.beat != null) StopCoroutine(this.beat);
            StopLoose();
            fx.HideAll();
            this.beat = null; beats = null; current = null;
            skipFrame = Time.frameCount;
            skip = false;
        }

        void OnLine(DialogueLine line)
        {
            if (current == null || beats == null) return;
            var lines = current.Lines;
            int index = -1;
            for (int i = 0; i < lines.Count; i++) if (ReferenceEquals(lines[i], line)) { index = i; break; }
            if (index < 0) return;
            if (beat != null) StopCoroutine(beat);
            StopLoose();
            fx.HideAll();
            Time.timeScale = 1f;
            var b = beats(index);
            beat = b != null ? StartCoroutine(b) : null;
        }

        // Slow motion for `seconds` of real time (eased in and out); actors and particles slow down, the camera work does not.
        protected IEnumerator SlowMotion(float scale, float seconds, float ease = 0.25f)
        {
            for (float t = 0f; t < ease; t += Time.unscaledDeltaTime) { Time.timeScale = Mathf.Lerp(1f, scale, t / ease); yield return null; }
            Time.timeScale = scale;
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, seconds - 2f * ease));
            for (float t = 0f; t < ease; t += Time.unscaledDeltaTime) { Time.timeScale = Mathf.Lerp(scale, 1f, t / ease); yield return null; }
            Time.timeScale = 1f;
        }

        // Until the player presses a key (Space / Enter / Esc / click): the card of an epilogue.
        protected IEnumerator WaitForPress()
        {
            skip = false;
            yield return null;
            while (!skip) yield return null;
            skip = false;
        }

        // Several things in sequence as one coroutine.
        protected static IEnumerator Seq(params IEnumerator[] steps)
        {
            foreach (var s in steps) yield return s;
        }

        protected IEnumerator Delay(float seconds, Action action)
        {
            yield return new WaitForSecondsRealtime(seconds);
            action();
        }

        protected IEnumerator Then(float seconds, IEnumerator next)
        {
            yield return new WaitForSecondsRealtime(seconds);
            yield return next;
        }

        // Starts a side routine (a camera move, an actor's walk) that is stopped when the line / chapter it belongs to ends.
        protected Coroutine Go(IEnumerator routine)
        {
            var co = StartCoroutine(routine);
            loose.Add(co);
            return co;
        }

        void StopLoose()
        {
            foreach (var co in loose) if (co != null) StopCoroutine(co);
            loose.Clear();
        }
    }
}
