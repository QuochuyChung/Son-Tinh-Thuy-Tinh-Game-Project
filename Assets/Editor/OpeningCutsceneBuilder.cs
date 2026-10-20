using System.Linq;
using SonTinhThuyTinh.Characters;
using SonTinhThuyTinh.Cutscene;
using SonTinhThuyTinh.Dialogue;
using SonTinhThuyTinh.Flow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SonTinhThuyTinh.EditorTools
{
    // Builds Assets/Scenes/Cutscene_Opening.unity, the 3D opening of the game (MainMenu -> this -> CharacterSelect), see
    // OpeningCutsceneDirector. The set and the cast come from CineSetBuilder; the text is the existing Dialogue_Prologue.
    // Also points the main menu's "new game" at this scene and adds it to Build Settings and the music table. Rebuilds everything each run.
    public static class OpeningCutsceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Cutscene_Opening.unity";
        const string PrologueText = "Assets/Data/Dialogue/Dialogue_Prologue.asset";
        const string DialoguePath = "Assets/Data/Dialogue/Dialogue_Opening.asset";
        const string MainMenuPath = "Assets/Scenes/MainMenu.unity";
        const string SonWalk = "Assets/Animations/SonTinh_v2/son_tinh_v2_walk.fbx";
        const string ThuyWalk = "Assets/Animations/ThuyTinh_v2/thuy_tinh_v2_walk.fbx";
        const string MiWalk = "Assets/Animations/MiNuong/mi_nuong_walk.fbx";

        [MenuItem("Tools/Son Tinh Thuy Tinh/Build Opening Cutscene")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("Stop Play mode first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string previous = SceneManager.GetActiveScene().path;

            WriteOpeningText();
            var scene = CineSetBuilder.CopyPalaceScene(ScenePath);
            var runner = CineSetBuilder.MoveDialogueUi(scene);

            var set = new GameObject("Cutscene").transform;
            var throne = CineSetBuilder.BuildThrone(set);
            var (cam, stage) = CineSetBuilder.BuildCameraAndStage(set);

            var marks = new GameObject("Marks").transform; marks.SetParent(set);
            Vector3 avenue = new(0f, 14f, 10f);
            var seat = CineSetBuilder.Mark(marks, "KingSeat", CineSetBuilder.DaisCenter + new Vector3(0f, CineSetBuilder.DaisTop, CineSetBuilder.SeatZ), avenue);
            var miMark = CineSetBuilder.Mark(marks, "MiNuong", new Vector3(-2.9f, 14f, 32.2f), new Vector3(0f, 14f, 22f));
            var sonMark = CineSetBuilder.Mark(marks, "SonTinh", new Vector3(-4.2f, 14f, 21f), new Vector3(0f, 14f, 33f));
            var thuyMark = CineSetBuilder.Mark(marks, "ThuyTinh", new Vector3(4.2f, 14f, 21f), new Vector3(0f, 14f, 33f));
            var sonExit = CineSetBuilder.Mark(marks, "SonTinhExit", new Vector3(-13f, 14f, 5f), new Vector3(-13f, 14f, 0f));
            var thuyExit = CineSetBuilder.Mark(marks, "ThuyTinhExit", new Vector3(13f, 14f, 5f), new Vector3(13f, 14f, 0f));

            var cast = new GameObject("Cast").transform; cast.SetParent(set);
            var king = CineSetBuilder.PlacePrefab(cast, CineSetBuilder.KingPrefab, null, seat.position, seat.rotation, "HungVuong");
            var mi = CineSetBuilder.PlacePrefab(cast, CineSetBuilder.MiNuongPrefab, MiWalk, miMark.position, miMark.rotation, "MiNuong");
            var son = CineSetBuilder.PlaceSuitor(cast, CharacterId.SonTinh, SonWalk, sonMark.position, sonMark.rotation, "SonTinh");
            var thuy = CineSetBuilder.PlaceSuitor(cast, CharacterId.ThuyTinh, ThuyWalk, thuyMark.position, thuyMark.rotation, "ThuyTinh");

            var directorGo = new GameObject("OpeningDirector"); directorGo.transform.SetParent(set);
            var director = directorGo.AddComponent<OpeningCutsceneDirector>();
            var so = new SerializedObject(director);
            so.FindProperty("runner").objectReferenceValue = runner;
            so.FindProperty("cam").objectReferenceValue = cam;
            so.FindProperty("stage").objectReferenceValue = stage;
            so.FindProperty("king").objectReferenceValue = king;
            so.FindProperty("miNuong").objectReferenceValue = mi;
            so.FindProperty("sonTinh").objectReferenceValue = son;
            so.FindProperty("thuyTinh").objectReferenceValue = thuy;
            so.FindProperty("kingSeat").objectReferenceValue = seat;
            so.FindProperty("kingStand").objectReferenceValue = CineSetBuilder.Mark(marks, "KingStand", new Vector3(0f, CineSetBuilder.StepTop(0f, 32.7f), 32.7f), avenue);
            so.FindProperty("miNuongMark").objectReferenceValue = miMark;
            so.FindProperty("sonTinhMark").objectReferenceValue = sonMark;
            so.FindProperty("thuyTinhMark").objectReferenceValue = thuyMark;
            so.FindProperty("sonTinhExit").objectReferenceValue = sonExit;
            so.FindProperty("thuyTinhExit").objectReferenceValue = thuyExit;
            so.FindProperty("prologue").objectReferenceValue = AssetDatabase.LoadAssetAtPath<DialogueSequence>(DialoguePath);
            so.FindProperty("nextScene").stringValue = SceneNames.CharacterSelect;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            CineSetBuilder.AddToBuildSettings(ScenePath, "Assets/Scenes/MainMenu.unity");
            PointMainMenuHere();

            if (!string.IsNullOrEmpty(previous) && previous != ScenePath) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
            Debug.Log("OpeningCutsceneBuilder: built " + ScenePath);
        }

        // Refresh only Thuy Tinh's embedded cutscene actor from the current character prefab.
        // The opening scene stores a detached copy of the visual rig, so changing the gameplay
        // prefab alone does not update the character shown in the story introduction.
        [MenuItem("Tools/Son Tinh Thuy Tinh/Thuy Tinh: Update Opening Story Model")]
        public static void UpdateThuyTinhActor()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("Stop Play mode first."); return; }

            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedForUpdate = !scene.IsValid() || !scene.isLoaded;
            if (openedForUpdate) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            try
            {
                var director = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<OpeningCutsceneDirector>(true))
                    .FirstOrDefault();
                if (director == null) throw new System.InvalidOperationException("OpeningCutsceneDirector is missing from " + ScenePath);

                var directorSo = new SerializedObject(director);
                var actorProperty = directorSo.FindProperty("thuyTinh");
                var oldActor = actorProperty.objectReferenceValue as ActorRig;
                if (oldActor == null) throw new System.InvalidOperationException("The opening cutscene has no Thuy Tinh actor assigned.");

                Transform parent = oldActor.transform.parent;
                Vector3 position = oldActor.transform.position;
                Quaternion rotation = oldActor.transform.rotation;
                int siblingIndex = oldActor.transform.GetSiblingIndex();
                bool active = oldActor.gameObject.activeSelf;

                var newActor = CineSetBuilder.PlaceSuitor(parent, CharacterId.ThuyTinh, ThuyWalk, position, rotation, "ThuyTinh");
                newActor.transform.SetSiblingIndex(siblingIndex);
                newActor.gameObject.SetActive(active);

                actorProperty.objectReferenceValue = newActor;
                directorSo.ApplyModifiedPropertiesWithoutUndo();
                Object.DestroyImmediate(oldActor.gameObject);

                const string expectedModel = "Assets/Art/Characters/ThuyTinh_AzureSovereign/thuy_tinh_azure_sovereign.fbx";
                bool usesAzureSovereign = newActor.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Any(renderer => renderer.sharedMesh != null && AssetDatabase.GetAssetPath(renderer.sharedMesh) == expectedModel);
                if (!usesAzureSovereign)
                    throw new System.InvalidOperationException("The refreshed opening actor does not use the Azure Sovereign model.");

                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new System.InvalidOperationException("Unity could not save " + ScenePath);

                Debug.Log("OpeningCutsceneBuilder: Thuy Tinh opening actor updated to Azure Sovereign; dialogue, camera and scene timing were preserved.");
            }
            finally
            {
                if (openedForUpdate && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        // Dialogue_Opening = the lines of Dialogue_Prologue, without their illustrations.
        static void WriteOpeningText()
        {
            var source = AssetDatabase.LoadAssetAtPath<DialogueSequence>(PrologueText);
            var lines = source.Lines.Select(l => (l.speaker, l.text)).ToArray();
            // line 9 is the bride-price: the game has six creatures (three of the mountain, three of the sea, each god fetches his own)
            lines[9] = ("Hùng Vương", "Sính lễ là sáu linh thú mang số chín: ba con ngự trên non cao, ba con ẩn dưới biển cả. Mỗi chàng đi lấy ba con của miền mình. Ai mang đủ tới trước, ta gả Mị Nương cho người ấy.");
            CineDialogue.Write(DialoguePath, lines);
        }

        // The main menu's new game goes to the opening cutscene instead of the old Prologue (which stays as it was).
        static void PointMainMenuHere()
        {
            var menu = EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);
            bool changed = false;
            foreach (var go in menu.GetRootGameObjects())
                foreach (var c in go.GetComponentsInChildren<SonTinhThuyTinh.UI.MainMenu.MainMenuController>(true))
                {
                    var so = new SerializedObject(c);
                    var p = so.FindProperty("firstScene");
                    if (p.stringValue == SceneNames.Opening) continue;
                    p.stringValue = SceneNames.Opening;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }
            if (changed) EditorSceneManager.SaveScene(menu);
        }
    }
}
