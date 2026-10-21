using System.Collections.Generic;
using System.IO;
using SonTinhThuyTinh.Trailer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SonTinhThuyTinh.EditorTools
{
    // Tools ▸ Son Tinh Thuy Tinh ▸ Trailer ▸ Record Draft Shots: for each trailer shot, opens its scene (never saved), enters Play mode
    // with the shot name in Trailer_Output/pending_shot.txt (TrailerShots picks it up on the first frame), waits for it to finish and
    // leaves Play mode. Frames go to Trailer_Output/frames/<shot>/; Assets/Trailer/Tools~/assemble_trailer.py turns them into the MP4.
    // Needs a GPU, so run it from an open Editor (or -executeMethod ...TrailerRecorder.RecordAll without -batchmode/-nographics).
    [InitializeOnLoad]
    public static class TrailerRecorder
    {
        public static readonly (string shot, string scene)[] Shots =
        {
            ("opening", "Assets/Scenes/Cutscene_Opening.unity"),
            ("palace", "Assets/Scenes/Map_HungVuong.unity"),
            ("sontinh_map", "Assets/Scenes/Map_SonTinh.unity"),
            ("thuytinh_map", "Assets/Scenes/Map_ThuyTinh.unity"),
            ("skills_son", "Assets/Scenes/Map_HungVuong.unity"),
            ("skills_thuy", "Assets/Scenes/Map_HungVuong.unity"),
            ("clash", "Assets/Scenes/Map_HungVuong.unity"),
            ("v2_palace", "Assets/Scenes/Map_HungVuong.unity"),
            ("v2_son", "Assets/Scenes/Map_SonTinh.unity"),
            ("v2_thuy", "Assets/Scenes/Map_ThuyTinh.unity"),
            ("v2_duel_son", "Assets/Scenes/Map_HungVuong.unity"),
            ("v2_duel_thuy", "Assets/Scenes/Map_HungVuong.unity"),
            ("v2_clash", "Assets/Scenes/Map_HungVuong.unity"),
            ("v2_fix_palace", "Assets/Scenes/Map_HungVuong.unity"),
            ("v2_fix_son", "Assets/Scenes/Map_SonTinh.unity"),
            ("v3_palace", "Assets/Scenes/Map_HungVuong.unity"),
            ("v3_beasts_son", "Assets/Scenes/Map_SonTinh.unity"),
            ("v3_beasts_thuy", "Assets/Scenes/Map_ThuyTinh.unity"),
            ("v3_horse_fix", "Assets/Scenes/Map_SonTinh.unity"),
            ("v4_tour_son", "Assets/Scenes/Map_SonTinh.unity"),
            ("v4_tour_thuy", "Assets/Scenes/Map_ThuyTinh.unity"),
            ("v4_clash_side", "Assets/Scenes/Map_HungVuong.unity"),
            ("v4_clash_high", "Assets/Scenes/Map_HungVuong.unity"),
            ("v5_hero_son", "Assets/Scenes/Map_SonTinh.unity"),
            ("v5_hero_thuy", "Assets/Scenes/Map_ThuyTinh.unity"),
            ("v3_turtle_fix_thuy", "Assets/Scenes/Map_ThuyTinh.unity"),
        };

        const string QueueKey = "Trailer.Queue";

        static TrailerRecorder()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        [MenuItem("Tools/Son Tinh Thuy Tinh/Trailer/Record Draft Shots")]
        public static void RecordAll()
        {
            var names = new List<string>();
            foreach (var s in Shots) names.Add(s.shot);
            Record(string.Join(",", names));
        }

        // comma-separated shot names, e.g. "opening,clash"
        public static void Record(string shots)
        {
            SessionState.SetString(QueueKey, shots);
            Debug.Log("[Trailer] queue " + shots);
        }

        static void Tick()
        {
            string queue = SessionState.GetString(QueueKey, "");
            if (queue.Length == 0 || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (EditorApplication.isPaused) EditorApplication.isPaused = false;   // Error Pause must not stall a recording
                // in Play mode: leave it once the shot is over (no capture running and nothing pending)
                if (EditorApplication.isPlaying && !File.Exists(TrailerShots.PendingFile) && !TrailerShots.Busy && Time.frameCount > 10)
                {
                    string[] rest = queue.Split(',');
                    SessionState.SetString(QueueKey, string.Join(",", rest, 1, rest.Length - 1));
                    EditorApplication.isPlaying = false;
                }
                return;
            }
            string shot = queue.Split(',')[0];
            string scene = null;
            foreach (var s in Shots) if (s.shot == shot) scene = s.scene;
            if (scene == null) { Debug.LogError("[Trailer] unknown shot " + shot); SessionState.SetString(QueueKey, ""); return; }
            EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);   // the previous scene is discarded, never saved
            Directory.CreateDirectory(Path.GetDirectoryName(TrailerShots.PendingFile));
            File.WriteAllText(TrailerShots.PendingFile, shot);
            EditorApplication.isPlaying = true;
        }
    }
}
