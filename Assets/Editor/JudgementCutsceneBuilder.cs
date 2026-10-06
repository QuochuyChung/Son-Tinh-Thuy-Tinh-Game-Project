using System.Collections.Generic;
using System.IO;
using System.Linq;
using SonTinhThuyTinh.Characters;
using SonTinhThuyTinh.Cutscene;
using SonTinhThuyTinh.Dialogue;
using SonTinhThuyTinh.Flow;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using Cue = SonTinhThuyTinh.Cutscene.JudgementCutsceneDirector.Cue;
using Shot = SonTinhThuyTinh.Cutscene.JudgementCutsceneDirector.Shot;
using KingAction = SonTinhThuyTinh.Cutscene.JudgementCutsceneDirector.KingAction;

namespace SonTinhThuyTinh.EditorTools
{
    // Builds Assets/Scenes/Cutscene_PhanXu.unity, Hùng Vương's judgement (game flow: Map_HungVuong -> this -> Sandbox_Combat until the arena exists).
    // The set is a copy of Map_HungVuong (plateau, palace, avenue, lighting) without the gameplay objects; Hùng Vương (prefab HungVuong) stands
    // at the foot of the palace steps, the two suitors (the visual part of their player prefabs, placed at runtime) face him on the avenue.
    // The dialogue UI is the Prologue's (moved over from that scene), Mị Nương appears as her Prologue illustration on her line.
    // Intro: Timeline with a Cinemachine track (crane over the avenue down to the wide shot); then JudgementCutsceneDirector cuts cameras and
    // starts the king's gestures line by line. Two dialogues, one per chosen character. Rebuilds everything on every run.
    public static class JudgementCutsceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Cutscene_PhanXu.unity";
        const string MapPath = "Assets/Scenes/Map_HungVuong.unity";
        const string ProloguePath = "Assets/Scenes/Prologue.unity";
        const string KingPrefab = "Assets/Prefabs/Characters/HungVuong.prefab";
        const string RosterPath = "Assets/Data/Characters/CharacterRoster.asset";
        const string MiNuongSprite = "Assets/Art/Story/02_mi_nuong.jpg";
        const string DialogueDir = "Assets/Data/Dialogue";
        const string TimelinePath = "Assets/Data/Cutscene/TL_PhanXu_Intro.playable";

        // the set, in Map_HungVuong coordinates: palace front (steps) at z = 36, avenue along +z, plateau top at y = 14
        static readonly Vector3 KingPos = new(0f, 14f, 33.6f);
        static readonly Vector3 SonTinhPos = new(-2.3f, 14f, 25.6f);
        static readonly Vector3 ThuyTinhPos = new(2.3f, 14f, 25.6f);
        const float Eye = 1.72f;   // head height of the 1.9 m characters

        // map objects that only make sense while playing
        static readonly string[] RemoveRoots = { "PlayerFollowCamera", "PlayerSpawn", "GameplayHUD", "PlayerHUD", "PauseCanvas", "PauseMenu", "MapRoute", "MapCanvas" };
        static readonly string[] RemoveGenerated = { "Exit_To_Next", "Spawns" };

        const string King = "Hùng Vương", MiNuong = "Mị Nương", SonTinh = "Sơn Tinh", ThuyTinh = "Thủy Tinh";

        [MenuItem("Tools/Son Tinh Thuy Tinh/Build Judgement Cutscene")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("Stop Play mode first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string previous = SceneManager.GetActiveScene().path;

            var dialogues = BuildDialogues();
            var timeline = BuildTimelineAsset();

            AssetDatabase.DeleteAsset(ScenePath);
            AssetDatabase.CopyAsset(MapPath, ScenePath);
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            foreach (var root in scene.GetRootGameObjects())
                if (RemoveRoots.Contains(root.name)) Object.DestroyImmediate(root);
            var generated = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Generated");
            if (generated != null)
                foreach (string n in RemoveGenerated) { var t = generated.transform.Find(n); if (t != null) Object.DestroyImmediate(t.gameObject); }
            foreach (var trigger in Object.FindObjectsByType<SceneTransitionTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(trigger.gameObject);

            DialogueRunner runner = MoveDialogueUi(scene);

            var brain = Object.FindFirstObjectByType<CinemachineBrain>();
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 0.7f);

            var set = new GameObject("Cutscene").transform;
            var king = PlaceKing(set);
            var sonMark = Mark(set, "Mark_SonTinh", SonTinhPos, KingPos);
            var thuyMark = Mark(set, "Mark_ThuyTinh", ThuyTinhPos, KingPos);

            var cams = new GameObject("Cameras").transform; cams.SetParent(set);
            Vector3 kingHead = KingPos + Vector3.up * Eye, sonHead = SonTinhPos + Vector3.up * Eye, thuyHead = ThuyTinhPos + Vector3.up * Eye;
            var craneHigh = Cam(cams, "CM_CraneHigh", new Vector3(0f, 31f, -12f), new Vector3(0f, 19f, 36f), 40f);
            var craneLow = Cam(cams, "CM_CraneLow", new Vector3(5f, 20f, 9f), new Vector3(0f, 16f, 33f), 40f);
            var wide = Cam(cams, "CM_Wide", new Vector3(0f, 16.7f, 16.5f), new Vector3(0f, 15.9f, 31f), 42f);
            var kingCam = Cam(cams, "CM_King", new Vector3(0.9f, 15.75f, 29.4f), kingHead + Vector3.down * 0.25f, 30f);
            var kingLow = Cam(cams, "CM_KingLow", new Vector3(-1.4f, 14.6f, 30.6f), kingHead + Vector3.up * 0.1f, 38f);
            var sonClose = Cam(cams, "CM_SonTinh", new Vector3(-1.0f, 15.85f, 28.6f), sonHead + Vector3.down * 0.15f, 32f);
            var thuyClose = Cam(cams, "CM_ThuyTinh", new Vector3(1.0f, 15.85f, 28.6f), thuyHead + Vector3.down * 0.15f, 32f);
            var suitors = Cam(cams, "CM_Suitors", new Vector3(1.3f, 16.3f, 35.4f), new Vector3(0f, 15.3f, 25.6f), 40f);
            wide.Priority = 10;   // live before the director takes over (and if the intro is missing)

            var directorGo = new GameObject("JudgementDirector"); directorGo.transform.SetParent(set);
            timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);   // opening the scene unloaded the in-memory copy
            var playable = directorGo.AddComponent<PlayableDirector>();
            playable.playableAsset = timeline; playable.playOnAwake = false; playable.extrapolationMode = DirectorWrapMode.None;
            var track = timeline.GetOutputTracks().OfType<CinemachineTrack>().First();
            playable.SetGenericBinding(track, brain);
            var shots = track.GetClips().ToArray();
            BindShot(playable, shots[0], craneHigh); BindShot(playable, shots[1], craneLow); BindShot(playable, shots[2], wide);

            var director = directorGo.AddComponent<JudgementCutsceneDirector>();
            var so = new SerializedObject(director);
            so.FindProperty("runner").objectReferenceValue = runner;
            so.FindProperty("intro").objectReferenceValue = playable;
            so.FindProperty("roster").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CharacterRoster>(RosterPath);
            so.FindProperty("sonTinhMark").objectReferenceValue = sonMark;
            so.FindProperty("thuyTinhMark").objectReferenceValue = thuyMark;
            so.FindProperty("king").objectReferenceValue = king;
            so.FindProperty("wide").objectReferenceValue = wide;
            so.FindProperty("kingCamera").objectReferenceValue = kingCam;
            so.FindProperty("kingLow").objectReferenceValue = kingLow;
            so.FindProperty("sonTinhClose").objectReferenceValue = sonClose;
            so.FindProperty("thuyTinhClose").objectReferenceValue = thuyClose;
            so.FindProperty("suitors").objectReferenceValue = suitors;
            so.FindProperty("nextScene").stringValue = SceneNames.Sandbox;   // the arena (section 8.8) replaces this later
            var scripts = so.FindProperty("scripts");
            scripts.arraySize = dialogues.Count;
            for (int i = 0; i < dialogues.Count; i++)
            {
                var e = scripts.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("player").enumValueIndex = (int)dialogues[i].player;
                e.FindPropertyRelative("dialogue").objectReferenceValue = AssetDatabase.LoadAssetAtPath<DialogueSequence>(dialogues[i].path);
                var cues = e.FindPropertyRelative("cues");
                cues.arraySize = dialogues[i].cues.Length;
                for (int c = 0; c < dialogues[i].cues.Length; c++)
                {
                    cues.GetArrayElementAtIndex(c).FindPropertyRelative("shot").enumValueIndex = (int)dialogues[i].cues[c].shot;
                    cues.GetArrayElementAtIndex(c).FindPropertyRelative("king").enumValueIndex = (int)dialogues[i].cues[c].king;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene);
            AddToBuildSettings();
            PointPalaceExitHere();

            if (!string.IsNullOrEmpty(previous) && previous != ScenePath) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
            Debug.Log("JudgementCutsceneBuilder: built " + ScenePath);
        }

        // ---------- dialogue ----------

        struct Built { public CharacterId player; public string path; public Cue[] cues; }

        static Cue C(Shot shot, KingAction king = KingAction.Keep) => new Cue { shot = shot, king = king };

        static List<Built> BuildDialogues()
        {
            var miNuong = AssetDatabase.LoadAssetAtPath<Sprite>(MiNuongSprite);
            var result = new List<Built>();
            foreach (CharacterId player in new[] { CharacterId.SonTinh, CharacterId.ThuyTinh })
            {
                bool son = player == CharacterId.SonTinh;
                string me = son ? SonTinh : ThuyTinh, rival = son ? ThuyTinh : SonTinh;
                string rivalTaunt = son
                    ? "Hô mưa gọi gió là việc của ta. Nước dâng tới đâu, núi rừng của ngươi chìm tới đó!"
                    : "Ngươi dâng nước cao một thước, ta dựng núi cao một trượng. Đừng mong qua được non cao!";
                string myAnswer = son
                    ? "Thần xin tuân lệnh. Núi Tản Viên chưa từng chịu lùi trước sóng nước nào."
                    : "Thần xin tuân lệnh. Biển cả rộng lớn, chẳng ngọn núi nào chặn nổi.";
                var lines = new (string speaker, string text, Sprite pic, Cue cue)[]
                {
                    ("", "Sáng sớm hôm sau, trước sân rồng thành Phong Châu, cả triều đình nín thở. Hai chàng trai đã đứng chờ, lễ vật bày đủ sau lưng.", null, C(Shot.Wide, KingAction.Idle)),
                    (King, "Hôm qua ta đã nói: voi chín ngà, gà chín cựa, ngựa chín hồng mao. Ai mang đủ đến trước, người ấy được rước Mị Nương.", null, C(Shot.King, KingAction.Talk)),
                    (me, $"Muôn tâu bệ hạ, thần là {me}. Voi chín ngà, gà chín cựa, ngựa chín hồng mao, thần đã tìm đủ, xin dâng lên bệ hạ.", null, C(Shot.Player, KingAction.Idle)),
                    (rival, $"Thần là {rival}. Lễ vật của thần cũng đã bày đủ trước sân rồng, chẳng thiếu một món.", null, C(Shot.Opponent)),
                    (King, "Lạ thay... Hai người cùng đến một lúc, lễ vật như nhau, chẳng ai kém ai.", null, C(Shot.Suitors, KingAction.Talk)),
                    (MiNuong, "Phụ vương... con xin nghe theo lời cha định đoạt.", miNuong, C(Shot.King, KingAction.Idle)),
                    (King, "Lời vua đã nói ra thì không thể rút lại. Nhưng con gái ta chỉ có một.", null, C(Shot.King, KingAction.Talk)),
                    (King, "Vậy hãy để trời đất làm chứng! Ra đàn tế sau cung điện, hai người đấu một trận. Ai thắng, người ấy rước Mị Nương về.", null, C(Shot.KingLow, KingAction.Point)),
                    (rival, rivalTaunt, null, C(Shot.Opponent, KingAction.Idle)),
                    (me, myAnswer, null, C(Shot.Player)),
                    (King, "Được! Trời đất chứng giám. Bắt đầu!", null, C(Shot.Wide, KingAction.Nod)),
                };

                string path = $"{DialogueDir}/Dialogue_PhanXu_{(son ? "SonTinh" : "ThuyTinh")}.asset";
                var asset = AssetDatabase.LoadAssetAtPath<DialogueSequence>(path);
                if (asset == null) { asset = ScriptableObject.CreateInstance<DialogueSequence>(); AssetDatabase.CreateAsset(asset, path); }
                var so = new SerializedObject(asset);
                var arr = so.FindProperty("lines");
                arr.arraySize = lines.Length;
                for (int i = 0; i < lines.Length; i++)
                {
                    var e = arr.GetArrayElementAtIndex(i);
                    e.FindPropertyRelative("speaker").stringValue = lines[i].speaker;
                    e.FindPropertyRelative("text").stringValue = lines[i].text;
                    e.FindPropertyRelative("illustration").objectReferenceValue = lines[i].pic;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
                result.Add(new Built { player = player, path = path, cues = lines.Select(l => l.cue).ToArray() });
            }
            AssetDatabase.SaveAssets();
            return result;
        }

        // ---------- timeline ----------

        static TimelineAsset BuildTimelineAsset()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(TimelinePath));
            AssetDatabase.DeleteAsset(TimelinePath);
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            AssetDatabase.CreateAsset(timeline, TimelinePath);
            var track = timeline.CreateTrack<CinemachineTrack>(null, "Cameras");
            // crane high over the avenue -> lower and closer -> the wide shot behind the suitors; overlaps are the blends
            AddShot(track, "Crane high", 0.0, 3.4);
            AddShot(track, "Crane low", 2.0, 3.6);
            AddShot(track, "Wide", 4.6, 2.4);
            AssetDatabase.SaveAssets();
            return timeline;
        }

        static void AddShot(CinemachineTrack track, string name, double start, double duration)
        {
            var clip = track.CreateClip<CinemachineShot>();
            clip.displayName = name; clip.start = start; clip.duration = duration;
            clip.blendInDuration = -1; clip.blendOutDuration = -1;   // automatic (from the overlaps)
        }

        static void BindShot(PlayableDirector director, TimelineClip clip, CinemachineCamera cam)
        {
            var shot = (CinemachineShot)clip.asset;
            if (shot.VirtualCamera.exposedName == default) shot.VirtualCamera.exposedName = GUID.Generate().ToString();
            director.SetReferenceValue(shot.VirtualCamera.exposedName, cam);
            EditorUtility.SetDirty(shot);
        }

        // ---------- set ----------

        static Animator PlaceKing(Transform set)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(KingPrefab));
            go.transform.SetParent(set);
            go.transform.SetPositionAndRotation(KingPos, Quaternion.LookRotation(new Vector3(0f, 0f, -1f)));
            return go.GetComponentInChildren<Animator>();
        }

        static Transform Mark(Transform set, string name, Vector3 pos, Vector3 lookAt)
        {
            var t = new GameObject(name).transform;
            t.SetParent(set);
            Vector3 dir = lookAt - pos; dir.y = 0f;
            t.SetPositionAndRotation(pos, Quaternion.LookRotation(dir));
            return t;
        }

        static CinemachineCamera Cam(Transform parent, string name, Vector3 pos, Vector3 lookAt, float fov)
        {
            var go = new GameObject(name); go.transform.SetParent(parent);
            go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(lookAt - pos));
            var cam = go.AddComponent<CinemachineCamera>();
            var lens = cam.Lens; lens.FieldOfView = fov; lens.NearClipPlane = 0.1f; lens.FarClipPlane = 1500f; cam.Lens = lens;
            cam.Priority = 0;
            return cam;
        }

        // The Prologue's dialogue canvas and runner, moved into this scene (the Prologue file itself is not saved, so it keeps them).
        static DialogueRunner MoveDialogueUi(Scene target)
        {
            var prologue = EditorSceneManager.OpenScene(ProloguePath, OpenSceneMode.Additive);
            var roots = prologue.GetRootGameObjects();
            var canvas = roots.First(g => g.name == "PrologueCanvas");
            var runnerGo = roots.First(g => g.GetComponent<DialogueRunner>() != null);
            SceneManager.MoveGameObjectToScene(canvas, target);
            SceneManager.MoveGameObjectToScene(runnerGo, target);
            EditorSceneManager.CloseScene(prologue, true);

            canvas.name = "DialogueCanvas";
            runnerGo.name = "Dialogue";
            var title = canvas.transform.Find("TitleCard");
            if (title != null) Object.DestroyImmediate(title.gameObject);
            var prologueDirector = runnerGo.GetComponent<PrologueDirector>();
            if (prologueDirector != null) Object.DestroyImmediate(prologueDirector);
            var runner = runnerGo.GetComponent<DialogueRunner>();
            var so = new SerializedObject(runner);
            so.FindProperty("keepIllustration").boolValue = false;   // Mị Nương's picture only on her line, the 3D scene otherwise
            so.ApplyModifiedPropertiesWithoutUndo();
            return runner;
        }

        // ---------- flow ----------

        static void AddToBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == ScenePath)) return;
            int map = scenes.FindIndex(s => s.path == MapPath);
            scenes.Insert(map >= 0 ? map + 1 : scenes.Count, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // Map_HungVuong's palace gate leads here now (PalaceMapBuilder sets the same on its next rebuild).
        static void PointPalaceExitHere()
        {
            var map = EditorSceneManager.OpenScene(MapPath, OpenSceneMode.Single);
            bool changed = false;
            foreach (var trigger in Object.FindObjectsByType<SceneTransitionTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var so = new SerializedObject(trigger);
                var name = so.FindProperty("sceneName");
                if (name.stringValue == SceneNames.JudgementCutscene) continue;
                name.stringValue = SceneNames.JudgementCutscene;
                so.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }
            if (changed) EditorSceneManager.SaveScene(map);
        }
    }
}
