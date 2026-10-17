using System;
using System.Collections;
using System.Collections.Generic;
using SonTinhThuyTinh.Dialogue;
using SonTinhThuyTinh.Player;
using SonTinhThuyTinh.Quest;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace SonTinhThuyTinh.Cutscene
{
    // A short fly-through at the start of a gift map (Map_SonTinh, Map_ThuyTinh): the narrator shows the player where the three gifts are
    // while a camera glides from one landmark to the next (docs/progress.md 9.36). Plays once per game, right after the map loads:
    //  - the game is held (QuestDialogue freezes time like every conversation) and the camera work runs in unscaled time;
    //  - one shot per line of `sequence`, started when the line appears (DialogueRunner.LineStarted); a line past the last shot keeps it;
    //  - the interface (HUD, map) is hidden for the duration and two black bars frame the picture; Space / Enter / click go to the next
    //    line, Esc / Start skip the rest (DialogueRunner), the voices are the pre-generated clips of DialogueVoicePlayer.
    // Built by Assets/Editor/MapIntroBuilder.cs (marks placed on the landmarks of the open scene).
    public class MapIntroCutscene : MonoBehaviour
    {
        [Serializable]
        public struct Shot
        {
            public Transform from, to, look;
            public float fovFrom, fovTo, seconds;
            [Tooltip("Pulls the camera in front of anything solid between it and the look point (use on still shots in tight spots).")]
            public bool clearView;
        }

        static readonly HashSet<string> seen = new();

        [SerializeField] string key = "intro";
        [SerializeField] DialogueRunner runner;
        [SerializeField] DialogueSequence sequence;
        [SerializeField] Shot[] shots;
        [Tooltip("Also counts as seen: SeaQuest's short opening narration of Map_ThuyTinh (the cutscene replaces it).")]
        [SerializeField] string replacesSeaIntro = "";
        [Tooltip("0 = plays as the scene starts. N > 0 = plays when the player has just collected the Nth gift (GiftTracker.Count reaches N).")]
        [SerializeField] int giftCountTrigger;
        [Tooltip("Seconds after the gift is collected (its banner shows meanwhile) before the film starts.")]
        [SerializeField] float triggerDelay = 3f;

        Camera cam;
        GameObject ui;
        CanvasGroup fade;
        readonly List<Canvas> hidden = new();
        Coroutine shotRoutine;

        public static void ResetAll() => seen.Clear();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => seen.Clear();

        IEnumerator Start()
        {
            if (seen.Contains(key) || sequence == null || runner == null) { Destroy(gameObject); yield break; }

            if (giftCountTrigger > 0)
            {
                // a later gift (or none of this map's) makes this one obsolete: only the film of the gift just collected plays
                while (GiftTracker.Count < giftCountTrigger) yield return null;
                if (GiftTracker.Count > giftCountTrigger) { seen.Add(key); Destroy(gameObject); yield break; }
                yield return new WaitForSeconds(triggerDelay);
                while (QuestDialogue.IsPlaying || runner.IsPlaying) yield return null;
            }
            seen.Add(key);
            if (!string.IsNullOrEmpty(replacesSeaIntro)) SeaQuest.MarkIntroSeen(replacesSeaIntro);

            yield return null;   // the player is spawned in Awake; one frame for the HUD and the quest keeper to appear
            yield return null;
            Build();
            HideInterface();
            yield return Fade(1f, 0f, 0.8f);

            bool done = false;
            runner.LineStarted += OnLine;
            QuestDialogue.Play(runner, sequence, () => done = true);
            while (!done) yield return null;
            runner.LineStarted -= OnLine;

            yield return Fade(0f, 1f, 0.5f);
            if (shotRoutine != null) StopCoroutine(shotRoutine);
            if (cam != null) Destroy(cam.gameObject);
            foreach (Canvas c in hidden) if (c != null) c.enabled = true;
            yield return Fade(1f, 0f, 0.6f);
            Destroy(ui);
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (runner != null) runner.LineStarted -= OnLine;
            if (cam != null) Destroy(cam.gameObject);
            if (ui != null) Destroy(ui);
            foreach (Canvas c in hidden) if (c != null) c.enabled = true;
        }

        void OnLine(DialogueLine line)
        {
            var lines = sequence.Lines;
            int index = -1;
            for (int i = 0; i < lines.Count; i++) if (ReferenceEquals(lines[i], line)) { index = i; break; }
            if (index < 0 || shots == null || shots.Length == 0) return;
            if (shotRoutine != null) StopCoroutine(shotRoutine);
            shotRoutine = StartCoroutine(Fly(shots[Mathf.Min(index, shots.Length - 1)]));
        }

        // The camera glides from `from` to `to` looking at `look`, eased, then stays there (a slight sway) until the next line.
        IEnumerator Fly(Shot s)
        {
            if (s.from == null || s.look == null) yield break;
            Transform to = s.to != null ? s.to : s.from;
            float seconds = Mathf.Max(0.5f, s.seconds);
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / seconds);
                Place(Vector3.Lerp(s.from.position, to.position, k), s.look.position, Mathf.Lerp(s.fovFrom, s.fovTo, k), s.clearView);
                yield return null;
            }
            while (true)
            {
                float sway = Mathf.Sin(Time.unscaledTime * 0.6f) * 0.3f;
                Place(to.position + Vector3.up * sway, s.look.position, s.fovTo, s.clearView);
                yield return null;
            }
        }

        void Place(Vector3 position, Vector3 look, float fov, bool clearView = false)
        {
            if (cam == null) return;
            if (clearView)
            {
                Vector3 toCamera = position - look;
                float distance = toCamera.magnitude;
                if (distance > 2.5f && Physics.Linecast(look, position, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
                    position = look + toCamera / distance * Mathf.Max(2.5f, hit.distance - 0.6f);
            }
            Vector3 d = look - position;
            cam.transform.SetPositionAndRotation(position, d.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(d) : cam.transform.rotation);
            cam.fieldOfView = fov > 1f ? fov : 50f;
        }

        // ---------------------------------------------------------------- camera and screen

        void Build()
        {
            Camera main = Camera.main;
            var go = new GameObject("MapIntroCamera");
            cam = go.AddComponent<Camera>();
            if (main != null) cam.CopyFrom(main);
            cam.depth = 100f;
            cam.targetTexture = null;
            cam.clearFlags = CameraClearFlags.Skybox;
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            if (main != null && main.TryGetComponent(out UniversalAdditionalCameraData mainData))
            {
                data.volumeLayerMask = mainData.volumeLayerMask;
                data.antialiasing = mainData.antialiasing;
                data.renderShadows = mainData.renderShadows;
            }
            if (shots != null && shots.Length > 0 && shots[0].from != null && shots[0].look != null) Place(shots[0].from.position, shots[0].look.position, shots[0].fovFrom);

            ui = new GameObject("MapIntroScreen", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            var canvas = ui.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = runner.PanelCanvas != null ? runner.PanelCanvas.sortingOrder - 1 : 40;   // under the dialogue box
            var scaler = ui.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            ui.GetComponent<CanvasGroup>().interactable = false;
            ui.GetComponent<CanvasGroup>().blocksRaycasts = false;
            Bar("TopBar", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), 120f);
            Bar("BottomBar", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), 120f);

            var curtain = new GameObject("Fade", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            curtain.transform.SetParent(ui.transform, false);
            var rt = (RectTransform)curtain.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            curtain.GetComponent<Image>().color = Color.black;
            curtain.GetComponent<Image>().raycastTarget = false;
            fade = curtain.GetComponent<CanvasGroup>();
            fade.alpha = 1f;
            fade.blocksRaycasts = false;
            fade.transform.SetAsLastSibling();
        }

        void Bar(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, float height)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(ui.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
            rt.sizeDelta = new Vector2(0f, height);
            rt.anchoredPosition = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = Color.black;
            img.raycastTarget = false;
        }

        // HUD, map, gift list ...: hidden while the film runs (the dialogue box and our own screen stay).
        void HideInterface()
        {
            Canvas keep = runner.PanelCanvas, mine = ui.GetComponent<Canvas>();
            foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (c == keep || c == mine || !c.enabled || c.renderMode == RenderMode.WorldSpace) continue;
                if (c.GetComponentInParent<DialogueRunner>() != null || c.name.Contains("Dialogue") || c.name.Contains("Pause") || c.name.Contains("SceneLoader")) continue;
                if (keep != null && c.transform.IsChildOf(keep.transform)) continue;
                c.enabled = false;
                hidden.Add(c);
            }
        }

        IEnumerator Fade(float from, float to, float seconds)
        {
            if (fade == null) yield break;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                fade.alpha = Mathf.Lerp(from, to, t / seconds);
                yield return null;
            }
            fade.alpha = to;
        }
    }
}
