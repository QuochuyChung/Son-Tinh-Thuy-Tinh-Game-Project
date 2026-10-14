using SonTinhThuyTinh.Characters;
using SonTinhThuyTinh.Cutscene;
using SonTinhThuyTinh.Dialogue;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SonTinhThuyTinh.EditorTools
{
    // Builds Assets/Scenes/Cutscene_Ending.unity, the ending after the final duel (Map_FinalBattle -> this -> MainMenu), see
    // EndingCutsceneDirector. The set and the cast come from CineSetBuilder; the two dialogues (one per winner) are written here.
    // The winner stands on the left mark, the loser on the right (the director swaps the characters), both upright. Rebuilds everything.
    public static class EndingCutsceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Cutscene_Ending.unity";
        const string DialogueDir = "Assets/Data/Dialogue";
        const string SonWalk = "Assets/Animations/SonTinh_v2/son_tinh_v2_walk.fbx";
        const string ThuyWalk = "Assets/Animations/ThuyTinh_v2/thuy_tinh_v2_walk.fbx";
        const string MiWalk = "Assets/Animations/MiNuong/mi_nuong_walk.fbx";
        const string King = "Hùng Vương", MiNuong = "Mị Nương", SonTinh = "Sơn Tinh", ThuyTinh = "Thủy Tinh";

        [MenuItem("Tools/Son Tinh Thuy Tinh/Build Ending Cutscene")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("Stop Play mode first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string previous = SceneManager.GetActiveScene().path;

            var sonWins = WriteDialogue(CharacterId.SonTinh);
            var thuyWins = WriteDialogue(CharacterId.ThuyTinh);

            var scene = CineSetBuilder.CopyPalaceScene(ScenePath);
            var runner = CineSetBuilder.MoveDialogueUi(scene);
            var set = new GameObject("Cutscene").transform;
            CineSetBuilder.BuildThrone(set);
            var (cam, stage) = CineSetBuilder.BuildCameraAndStage(set);

            var marks = new GameObject("Marks").transform; marks.SetParent(set);
            Vector3 avenue = new(0f, 14f, 10f);
            var kingMark = CineSetBuilder.Mark(marks, "King", new Vector3(0f, 14.5f, 32.6f), avenue);
            var miMark = CineSetBuilder.Mark(marks, "MiNuong", new Vector3(-2.9f, 14f, 32.2f), new Vector3(0f, 14f, 22f));
            // the duel is over: the two stand facing each other a little turned towards the king
            var winnerMark = CineSetBuilder.Mark(marks, "Winner", new Vector3(-2.3f, 14f, 24.5f), new Vector3(1.2f, 14f, 29f));
            var loserMark = CineSetBuilder.Mark(marks, "Loser", new Vector3(2.3f, 14f, 24.5f), new Vector3(-1.2f, 14f, 29f));
            var miWalkTo = CineSetBuilder.Mark(marks, "MiNuongWalkTo", new Vector3(-0.9f, 14f, 26.4f), new Vector3(-2.3f, 14f, 24.5f));

            var cast = new GameObject("Cast").transform; cast.SetParent(set);
            var king = CineSetBuilder.PlacePrefab(cast, CineSetBuilder.KingPrefab, null, kingMark.position, kingMark.rotation, "HungVuong");
            var mi = CineSetBuilder.PlacePrefab(cast, CineSetBuilder.MiNuongPrefab, MiWalk, miMark.position, miMark.rotation, "MiNuong");
            var son = CineSetBuilder.PlaceSuitor(cast, CharacterId.SonTinh, SonWalk, winnerMark.position, winnerMark.rotation, "SonTinh");
            var thuy = CineSetBuilder.PlaceSuitor(cast, CharacterId.ThuyTinh, ThuyWalk, loserMark.position, loserMark.rotation, "ThuyTinh");

            var directorGo = new GameObject("EndingDirector"); directorGo.transform.SetParent(set);
            var director = directorGo.AddComponent<EndingCutsceneDirector>();
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
            so.FindProperty("miNuongWalkTo").objectReferenceValue = miWalkTo;
            so.FindProperty("winnerMark").objectReferenceValue = winnerMark;
            so.FindProperty("loserMark").objectReferenceValue = loserMark;
            so.FindProperty("sonTinhWins").objectReferenceValue = AssetDatabase.LoadAssetAtPath<DialogueSequence>(sonWins);
            so.FindProperty("thuyTinhWins").objectReferenceValue = AssetDatabase.LoadAssetAtPath<DialogueSequence>(thuyWins);
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            CineSetBuilder.AddToBuildSettings(ScenePath, "Assets/Scenes/Map_FinalBattle.unity");

            if (!string.IsNullOrEmpty(previous) && previous != ScenePath) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
            Debug.Log("EndingCutsceneBuilder: built " + ScenePath);
        }

        // player = the character that WON the duel
        static string WriteDialogue(CharacterId player)
        {
            bool son = player == CharacterId.SonTinh;
            string me = son ? SonTinh : ThuyTinh, rival = son ? ThuyTinh : SonTinh;
            string winnerLine = son
                ? "Thần xin tuân lệnh bệ hạ. Núi Tản Viên sẽ là nơi thần che chở cho Mị Nương suốt đời."
                : "Thần xin tuân lệnh bệ hạ. Biển trời rộng lớn, thần sẽ che chở cho Mị Nương suốt đời.";
            string loserLine = son
                ? "Thần xin chịu thua. Nước dâng rồi cũng rút, thần không oán trách, xin lui về lo việc của mình."
                : "Thần xin chịu thua. Núi cao rồi cũng đứng đó, thần không oán trách, xin lui về lo việc của mình.";
            var lines = new (string speaker, string text)[]
            {
                ("", $"Trận chiến trên pháp đàn đã kết thúc. {rival} cúi đầu, {me} vẫn đứng thẳng trước điện."),
                (King, $"Trận đấu đã phân định. {me} là người thắng. Trời đất chứng giám, lời ta không rút lại."),
                (me, winnerLine),
                (rival, loserLine),
                (MiNuong, "Chàng... thiếp xin cảm ơn chàng. Mong rằng từ nay núi sông hòa bình, muôn dân yên vui."),
                (King, "Hay lắm! Các con đã vì muôn dân mà giao đấu. Hôm nay mở tiệc mừng, cả nước cùng vui!"),
                (King, "Từ nay, núi sông là một. Hai con hãy cùng nhau gìn giữ bờ cõi."),
                ("", $"Vậy là từ nay, {me} cùng Mị Nương sống hạnh phúc. Núi sông yên vui, muôn dân ấm no."),
            };
            return CineDialogue.Write($"{DialogueDir}/Dialogue_Ending_{(son ? "SonTinh" : "ThuyTinh")}.asset", lines);
        }
    }
}
