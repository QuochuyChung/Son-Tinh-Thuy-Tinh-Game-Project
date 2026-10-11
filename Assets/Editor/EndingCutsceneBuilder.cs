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
using Cue = SonTinhThuyTinh.Cutscene.EndingCutsceneDirector.Cue;
using Shot = SonTinhThuyTinh.Cutscene.EndingCutsceneDirector.Shot;
using KingAction = SonTinhThuyTinh.Cutscene.EndingCutsceneDirector.KingAction;
using MnAction = SonTinhThuyTinh.Cutscene.EndingCutsceneDirector.MiNuongAction;

namespace SonTinhThuyTinh.EditorTools
{
    // Builds Assets/Scenes/Cutscene_Ending.unity, the ending after the final duel (Map_FinalBattle -> this -> MainMenu).
    // The set is a copy of Map_HungVuong without the gameplay objects; Hùng Vương stands at the foot of the palace steps with Mị Nương
    // at his side, the winner (left mark) and the loser (right mark) face him on the avenue — BOTH standing, neither kneels.
    // Two dialogues, one per winning character. Intro: Timeline with a Cinemachine crane down to the wide shot; then
    // EndingCutsceneDirector cuts cameras and starts the king's gestures line by line, and shows the epilogue card at the end.
    // Rebuilds everything on every run.
    public static class EndingCutsceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Cutscene_Ending.unity";
        const string MapPath = "Assets/Scenes/Map_HungVuong.unity";
        const string ProloguePath = "Assets/Scenes/Prologue.unity";
        const string KingPrefab = "Assets/Prefabs/Characters/HungVuong.prefab";
        const string MiNuongPrefab = "Assets/Prefabs/Characters/MiNuong.prefab";   // optional: without it her lines show her illustration
        const string RosterPath = "Assets/Data/Characters/CharacterRoster.asset";
        const string MiNuongSprite = "Assets/Art/Story/02_mi_nuong.jpg";
        const string DialogueDir = "Assets/Data/Dialogue";
        const string TimelinePath = "Assets/Data/Cutscene/TL_Ending_Intro.playable";

        // the set, in Map_HungVuong coordinates: palace front (steps) at z = 36, avenue along +z, plateau top at y = 14
        static readonly Vector3 KingPos = new(0f, 14f, 33.6f);
        static readonly Vector3 MiNuongPos = new(-1.7f, 14f, 34.1f);   // at the king's right hand, a little behind him
        static readonly Vector3 WinnerPos = new(-2.3f, 14f, 25.6f);    // left of the avenue
        static readonly Vector3 LoserPos = new(2.3f, 14f, 25.6f);      // right of the avenue
        const float Eye = 1.72f;   // head height of the 1.9 m characters

        // map objects that only make sense while playing
        static readonly string[] RemoveRoots = { "PlayerFollowCamera", "PlayerSpawn", "GameplayHUD", "PlayerHUD", "PauseCanvas", "PauseMenu", "MapRoute", "MapCanvas" };
        static readonly string[] RemoveGenerated = { "Exit_To_Next", "Spawns" };

        const string King = "Hùng Vương", MiNuong = "Mị Nương", SonTinh = "Sơn Tinh", ThuyTinh = "Thủy Tinh";

        [MenuItem("Tools/Son Tinh Thuy Tinh/Build Ending Cutscene")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("Stop Play mode first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string previous = SceneManager.GetActiveScene().path;

            bool miNuong3d = AssetDatabase.LoadAssetAtPath<GameObject>(MiNuongPrefab) != null;
            var dialogues = BuildDialogues(miNuong3d);
            var timeline = BuildTimelineAsset();

            // a fresh copy of the map, written over the old scene file so the scene keeps its GUID (Build Settings, references)
            if (!File.Exists(ScenePath)) AssetDatabase.CopyAsset(MapPath, ScenePath);
            else { File.Copy(MapPath, ScenePath, true); AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceUpdate); }
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
            var king = Place(set, KingPrefab, KingPos, KingPos + new Vector3(0f, 0f, -1f));
            Animator miNuong = miNuong3d ? Place(set, MiNuongPrefab, MiNuongPos, new Vector3(0f, 14f, 25.6f)) : null;
            var winnerMark = Mark(set, "Mark_Winner", WinnerPos, KingPos);
            var loserMark = Mark(set, "Mark_Loser", LoserPos, KingPos);

            var cams = new GameObject("Cameras").transform; cams.SetParent(set);
            Vector3 kingHead = KingPos + Vector3.up * Eye, winnerHead = WinnerPos + Vector3.up * Eye, loserHead = LoserPos + Vector3.up * Eye;
            var craneHigh = Cam(cams, "CM_CraneHigh", new Vector3(0f, 31f, -12f), new Vector3(0f, 19f, 36f), 40f);
            var craneLow = Cam(cams, "CM_CraneLow", new Vector3(5f, 20f, 9f), new Vector3(0f, 16f, 33f), 40f);
            var wide = Cam(cams, "CM_Wide", new Vector3(0f, 16.7f, 16.5f), new Vector3(0f, 15.9f, 31f), 42f);
            var kingCam = Cam(cams, "CM_King", new Vector3(0.9f, 15.75f, 29.4f), kingHead + Vector3.down * 0.25f, 30f);
            var kingLow = Cam(cams, "CM_KingLow", new Vector3(-1.4f, 14.6f, 30.6f), kingHead + Vector3.up * 0.1f, 38f);
            var winnerClose = Cam(cams, "CM_Winner", new Vector3(-1.0f, 15.85f, 28.6f), winnerHead + Vector3.down * 0.15f, 32f);
            var loserClose = Cam(cams, "CM_Loser", new Vector3(1.0f, 15.85f, 28.6f), loserHead + Vector3.down * 0.15f, 32f);
            var couple = Cam(cams, "CM_Couple", new Vector3(1.3f, 16.3f, 35.4f), new Vector3(0f, 15.3f, 25.6f), 40f);
            // Mị Nương is 1.70 m: her eyes are ~0.18 m lower than the men's
            Vector3 mnHead = MiNuongPos + Vector3.up * (Eye - 0.18f);
            var mnCam = miNuong3d ? Cam(cams, "CM_MiNuong", new Vector3(-1.0f, 15.55f, 31.2f), mnHead + Vector3.down * 0.3f, 22f) : null;
            wide.Priority = 10;   // live before the director takes over (and if the intro is missing)

            var directorGo = new GameObject("EndingDirector"); directorGo.transform.SetParent(set);
            timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);   // opening the scene unloaded the in-memory copy
            var playable = directorGo.AddComponent<PlayableDirector>();
            playable.playableAsset = timeline; playable.playOnAwake = false; playable.extrapolationMode = DirectorWrapMode.None;
            var track = timeline.GetOutputTracks().OfType<CinemachineTrack>().First();
            playable.SetGenericBinding(track, brain);
            var shots = track.GetClips().ToArray();
            BindShot(playable, shots[0], craneHigh); BindShot(playable, shots[1], craneLow); BindShot(playable, shots[2], wide);

            var director = directorGo.AddComponent<EndingCutsceneDirector>();
            var so = new SerializedObject(director);
            so.FindProperty("runner").objectReferenceValue = runner;
            so.FindProperty("intro").objectReferenceValue = playable;
            so.FindProperty("roster").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CharacterRoster>(RosterPath);
            so.FindProperty("winnerMark").objectReferenceValue = winnerMark;
            so.FindProperty("loserMark").objectReferenceValue = loserMark;
            so.FindProperty("king").objectReferenceValue = king;
            so.FindProperty("miNuong").objectReferenceValue = miNuong;
            so.FindProperty("wide").objectReferenceValue = wide;
            so.FindProperty("kingCamera").objectReferenceValue = kingCam;
            so.FindProperty("kingLow").objectReferenceValue = kingLow;
            so.FindProperty("winnerClose").objectReferenceValue = winnerClose;
            so.FindProperty("loserClose").objectReferenceValue = loserClose;
            so.FindProperty("couple").objectReferenceValue = couple;
            so.FindProperty("miNuongCamera").objectReferenceValue = mnCam;
            so.FindProperty("nextScene").stringValue = SceneNames.MainMenu;
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
                    cues.GetArrayElementAtIndex(c).FindPropertyRelative("miNuong").enumValueIndex = (int)dialogues[i].cues[c].miNuong;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene);
            AddToBuildSettings();

            if (!string.IsNullOrEmpty(previous) && previous != ScenePath) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
            Debug.Log("EndingCutsceneBuilder: built " + ScenePath);
        }

        // ---------- dialogue ----------

        struct Built { public CharacterId player; public string path; public Cue[] cues; }

        static Cue C(Shot shot, KingAction king = KingAction.Keep, MnAction mn = MnAction.Keep) => new Cue { shot = shot, king = king, miNuong = mn };

        // With her 3D model Mị Nương's lines cut to her close-up; without it they show her Prologue illustration over the king's shot.
        static List<Built> BuildDialogues(bool miNuong3d)
        {
            var miNuong = miNuong3d ? null : AssetDatabase.LoadAssetAtPath<Sprite>(MiNuongSprite);
            var mnShot = miNuong3d ? Shot.MiNuong : Shot.King;
            var result = new List<Built>();
            foreach (CharacterId player in new[] { CharacterId.SonTinh, CharacterId.ThuyTinh })
            {
                bool son = player == CharacterId.SonTinh;
                string me = son ? SonTinh : ThuyTinh, rival = son ? ThuyTinh : SonTinh;
                // the loser stands at the right mark, the winner at the left: both upright, neither kneels
                string winnerLine = son
                    ? "Thần xin tuân lệnh bệ hạ. Núi Tản Viên sẽ là nơi thần che chở cho Mị Nương suốt đời."
                    : "Thần xin tuân lệnh bệ hạ. Biển trời rộng lớn, thần sẽ che chở cho Mị Nương suốt đời.";
                string loserLine = son
                    ? "Thần xin chịu thua. Nước dâng rồi cũng rút, thần không oán trách, xin lui về lo việc của mình."
                    : "Thần xin chịu thua. Núi cao rồi cũng đứng đó, thần không oán trách, xin lui về lo việc của mình.";
                var lines = new (string speaker, string text, Sprite pic, Cue cue)[]
                {
                    ("", $"Trận chiến trên pháp đàn đã kết thúc. {rival} cúi đầu, {me} vẫn đứng thẳng trước điện.", null, C(Shot.Wide, KingAction.Idle, MnAction.Idle)),
                    (King, $"Trận đấu đã phân định. {me} là người thắng. Trời đất chứng giám, lời ta không rút lại.", null, C(Shot.King, KingAction.Talk, MnAction.Shy)),
                    (me, winnerLine, null, C(Shot.Winner, KingAction.Idle)),
                    (rival, loserLine, null, C(Shot.Loser, KingAction.Idle)),
                    (MiNuong, "Chàng... thiếp xin cảm ơn chàng. Mong rằng từ nay núi sông hòa bình, muôn dân yên vui.", miNuong, C(mnShot, KingAction.Idle, MnAction.Talk)),
                    (King, "Hay lắm! Các con đã vì muôn dân mà giao đấu. Hôm nay mở tiệc mừng, cả nước cùng vui!", null, C(Shot.KingLow, KingAction.Point, MnAction.Bow)),
                    (King, "Từ nay, núi sông là một. Hai con hãy cùng nhau gìn giữ bờ cõi.", null, C(Shot.King, KingAction.Nod, MnAction.Idle)),
                    ("", $"Vậy là từ nay, {me} cùng Mị Nương sống hạnh phúc. Núi sông yên vui, muôn dân ấm no.", null, C(Shot.Couple, KingAction.Keep, MnAction.Keep)),
                };

                string path = $"{DialogueDir}/Dialogue_Ending_{(son ? "SonTinh" : "ThuyTinh")}.asset";
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
            // emptied and refilled rather than deleted, so the asset keeps its GUID
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);
            if (timeline == null) { timeline = ScriptableObject.CreateInstance<TimelineAsset>(); AssetDatabase.CreateAsset(timeline, TimelinePath); }
            foreach (var old in timeline.GetRootTracks().ToList()) timeline.DeleteTrack(old);
            var track = timeline.CreateTrack<CinemachineTrack>(null, "Cameras");
            // crane high over the avenue -> lower and closer -> the wide shot behind the standing pair; overlaps are the blends
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

        static Animator Place(Transform set, string prefab, Vector3 pos, Vector3 lookAt)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefab));
            go.transform.SetParent(set);
            Vector3 dir = lookAt - pos; dir.y = 0f;
            go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(dir));
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
            int battle = scenes.FindIndex(s => s.path == "Assets/Scenes/Map_FinalBattle.unity");
            scenes.Insert(battle >= 0 ? battle + 1 : scenes.Count, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
