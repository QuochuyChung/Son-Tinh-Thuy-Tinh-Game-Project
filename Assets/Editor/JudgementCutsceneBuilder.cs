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
    // Builds Assets/Scenes/Cutscene_PhanXu.unity, Hùng Vương's judgement (game flow: Map_HungVuong -> this -> Map_FinalBattle), see
    // JudgementCutsceneDirector. The set and the cast come from CineSetBuilder; the two dialogues (one per chosen character) are written
    // here. Rebuilds everything on every run.
    public static class JudgementCutsceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Cutscene_PhanXu.unity";
        const string DialogueDir = "Assets/Data/Dialogue";
        const string SonWalk = "Assets/Animations/SonTinh_v2/son_tinh_v2_walk.fbx";
        const string ThuyWalk = "Assets/Animations/ThuyTinh_v2/thuy_tinh_v2_walk.fbx";
        const string MiWalk = "Assets/Animations/MiNuong/mi_nuong_walk.fbx";
        const string King = "Hùng Vương", MiNuong = "Mị Nương", SonTinh = "Sơn Tinh", ThuyTinh = "Thủy Tinh";

        [MenuItem("Tools/Son Tinh Thuy Tinh/Build Judgement Cutscene")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("Stop Play mode first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string previous = SceneManager.GetActiveScene().path;

            var sonScript = WriteDialogue(CharacterId.SonTinh);
            var thuyScript = WriteDialogue(CharacterId.ThuyTinh);

            var scene = CineSetBuilder.CopyPalaceScene(ScenePath);
            var runner = CineSetBuilder.MoveDialogueUi(scene);
            var set = new GameObject("Cutscene").transform;
            CineSetBuilder.BuildThrone(set);
            var (cam, stage) = CineSetBuilder.BuildCameraAndStage(set);

            var marks = new GameObject("Marks").transform; marks.SetParent(set);
            Vector3 avenue = new(0f, 14f, 10f), palace = new(0f, 14f, 34f);
            var kingMark = CineSetBuilder.Mark(marks, "King", new Vector3(0f, CineSetBuilder.StepTop(0f, 32.6f), 32.6f), avenue);
            var miMark = CineSetBuilder.Mark(marks, "MiNuong", new Vector3(-2.9f, 14f, 32.2f), new Vector3(0f, 14f, 22f));
            var sonMark = CineSetBuilder.Mark(marks, "SonTinh", new Vector3(-2.3f, 14f, 25.6f), palace);
            var thuyMark = CineSetBuilder.Mark(marks, "ThuyTinh", new Vector3(2.3f, 14f, 25.6f), palace);

            var cast = new GameObject("Cast").transform; cast.SetParent(set);
            var king = CineSetBuilder.PlacePrefab(cast, CineSetBuilder.KingPrefab, null, kingMark.position, kingMark.rotation, "HungVuong");
            var mi = CineSetBuilder.PlacePrefab(cast, CineSetBuilder.MiNuongPrefab, MiWalk, miMark.position, miMark.rotation, "MiNuong");
            var son = CineSetBuilder.PlaceSuitor(cast, CharacterId.SonTinh, SonWalk, sonMark.position, sonMark.rotation, "SonTinh");
            var thuy = CineSetBuilder.PlaceSuitor(cast, CharacterId.ThuyTinh, ThuyWalk, thuyMark.position, thuyMark.rotation, "ThuyTinh");

            var directorGo = new GameObject("JudgementDirector"); directorGo.transform.SetParent(set);
            var director = directorGo.AddComponent<JudgementCutsceneDirector>();
            var so = new SerializedObject(director);
            so.FindProperty("runner").objectReferenceValue = runner;
            so.FindProperty("cam").objectReferenceValue = cam;
            so.FindProperty("stage").objectReferenceValue = stage;
            so.FindProperty("king").objectReferenceValue = king;
            so.FindProperty("miNuong").objectReferenceValue = mi;
            so.FindProperty("sonTinh").objectReferenceValue = son;
            so.FindProperty("thuyTinh").objectReferenceValue = thuy;
            so.FindProperty("kingMark").objectReferenceValue = kingMark;
            so.FindProperty("miNuongMark").objectReferenceValue = miMark;
            so.FindProperty("sonTinhMark").objectReferenceValue = sonMark;
            so.FindProperty("thuyTinhMark").objectReferenceValue = thuyMark;
            so.FindProperty("sonTinhScript").objectReferenceValue = AssetDatabase.LoadAssetAtPath<DialogueSequence>(sonScript);
            so.FindProperty("thuyTinhScript").objectReferenceValue = AssetDatabase.LoadAssetAtPath<DialogueSequence>(thuyScript);
            so.FindProperty("nextScene").stringValue = SceneNames.FinalBattle;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            CineSetBuilder.AddToBuildSettings(ScenePath, CineSetBuilder.MapPath);
            PointPalaceExitHere();

            if (!string.IsNullOrEmpty(previous) && previous != ScenePath) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
            Debug.Log("JudgementCutsceneBuilder: built " + ScenePath);
        }

        // me = the chosen character, rival = the other one
        static string WriteDialogue(CharacterId player)
        {
            bool son = player == CharacterId.SonTinh;
            string me = son ? SonTinh : ThuyTinh, rival = son ? ThuyTinh : SonTinh;
            string myRealm = son ? "non cao" : "biển cả", rivalRealm = son ? "biển cả" : "non cao";
            string rivalTaunt = son
                ? "Hô mưa gọi gió là việc của ta. Nước dâng tới đâu, núi rừng của ngươi chìm tới đó!"
                : "Ngươi dâng nước cao một thước, ta dựng núi cao một trượng. Đừng mong qua được non cao!";
            string myAnswer = son
                ? "Thần xin tuân lệnh. Núi Tản Viên chưa từng chịu lùi trước sóng nước nào."
                : "Thần xin tuân lệnh. Biển cả rộng lớn, chẳng ngọn núi nào chặn nổi.";
            var lines = new (string speaker, string text)[]
            {
                ("", "Sáng sớm hôm sau, trước sân rồng thành Phong Châu, cả triều đình nín thở. Hai chàng trai đã đứng chờ, lễ vật bày đủ sau lưng."),
                (King, "Hôm qua ta đã nói: mỗi chàng đi lấy ba linh thú của miền mình, ai mang đủ đến trước, người ấy được rước Mị Nương."),
                (me, $"Muôn tâu bệ hạ, thần là {me}. Ba linh thú miền {myRealm}, thần đã tìm đủ, xin dâng lên bệ hạ."),
                (rival, $"Thần là {rival}. Ba linh thú miền {rivalRealm} của thần cũng đã bày đủ trước sân rồng, chẳng thiếu một con."),
                (King, "Lạ thay... Hai người cùng đến một lúc, sính lễ đều đủ cả, chẳng ai kém ai."),
                (MiNuong, "Phụ vương... con xin nghe theo lời cha định đoạt."),
                (King, "Lời vua đã nói ra thì không thể rút lại. Nhưng con gái ta chỉ có một."),
                (King, "Vậy hãy để trời đất làm chứng! Ra đàn tế sau cung điện, hai người đấu một trận. Ai thắng, người ấy rước Mị Nương về."),
                (rival, rivalTaunt),
                (me, myAnswer),
                (MiNuong, "Xin hai chàng giữ lời, đấu cho công bằng. Thiếp sẽ chờ người chiến thắng."),
                (King, "Được! Trời đất chứng giám. Bắt đầu!"),
            };
            return CineDialogue.Write($"{DialogueDir}/Dialogue_PhanXu_{(son ? "SonTinh" : "ThuyTinh")}.asset", lines);
        }

        // Map_HungVuong's palace gate leads here (PalaceMapBuilder sets the same on its next rebuild).
        static void PointPalaceExitHere()
        {
            var map = EditorSceneManager.OpenScene(CineSetBuilder.MapPath, OpenSceneMode.Single);
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

    // Writes a DialogueSequence asset from (speaker, text) pairs, keeping the asset (and its GUID) if it exists.
    public static class CineDialogue
    {
        public static string Write(string path, (string speaker, string text)[] lines)
        {
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
                e.FindPropertyRelative("illustration").objectReferenceValue = null;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return path;
        }
    }
}
