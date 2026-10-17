using System.Collections.Generic;
using SonTinhThuyTinh.Cutscene;
using SonTinhThuyTinh.Dialogue;
using SonTinhThuyTinh.Quest;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SonTinhThuyTinh.EditorTools
{
    // Builds the map films (MapIntroCutscene, docs/progress.md 9.36 and 9.37) in the open scene, plus the Người Giữ Nhiệm Vụ marks:
    //  - Map_SonTinh / Map_ThuyTinh: "MapIntro" (as the map starts: overview, the keeper, the three gifts, the gate), "MapGift1" and
    //    "MapGift2" (right after the 1st / 2nd gift: the next keeper and the next stage), "MapGiftsDone" (all three: the gate), and
    //    three QuestKeeperMarks, one at the start of each leg;
    //  - Map_HungVuong: "MapPalace" (where the player stands in the story, the way to the court);
    //  - Map_FinalBattle: "MapFinalIntro" (the valley, the statues, the controls, the second phase).
    // Only the "MapFilms" and "QuestKeepers" objects and the Dialogue_*.asset files are touched, so the map builders do not run again;
    // running it twice replaces them. Shot marks are placed from (x, height over the ground or water, z) so they follow the terrain.
    public static class MapIntroBuilder
    {
        struct Mark
        {
            public Vector3 from, to, look;   // x, height over the ground (min the water level), z; for keeper shots: x right, y up, z forward of the keeper
            public float fovFrom, fovTo, seconds;
            public bool clearView;
            public int keeper;               // >= 0: the three vectors are in that keeper mark's local space
            public string line, speaker;
        }

        struct Film
        {
            public string name, asset, key, replacesSeaIntro;
            public int giftTrigger;
            public Mark[] marks;
        }

        [MenuItem("Tools/Son Tinh Thuy Tinh/Build Map Intro (open gift map)")]
        public static void Build()
        {
            string scene = EditorSceneManager.GetActiveScene().name;
            if (scene == "Map_SonTinh") BuildGiftMap("SonTinh", true);
            else if (scene == "Map_ThuyTinh") BuildGiftMap("ThuyTinh", false);
            else if (scene == "Map_HungVuong") BuildFilms(new[] { PalaceFilm() }, 0f, null);
            else if (scene == "Map_FinalBattle") BuildFilms(new[] { FinalFilm() }, 0.2f, null);
            else Debug.LogWarning("[MapIntro] open Map_SonTinh, Map_ThuyTinh, Map_HungVuong or Map_FinalBattle first (now: " + scene + ")");
        }

        // ---------------------------------------------------------------- the two gift maps

        static void BuildGiftMap(string map, bool sonTinh)
        {
            Terrain terrain = Terrain.activeTerrain;
            float water = sonTinh ? 0f : 2.4f;
            Transform[] keepers = BuildKeeperMarks(terrain, sonTinh);
            Film[] films = sonTinh ? SonTinhFilms() : ThuyTinhFilms();
            BuildFilms(films, water, keepers);
        }

        // One keeper at the start of each leg: the first beside the entry point (as before), the others on the main path just before the
        // arena of their gift, on dry, flat, free ground.
        static Transform[] BuildKeeperMarks(Terrain terrain, bool sonTinh)
        {
            GameObject old = GameObject.Find("QuestKeepers");
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject("QuestKeepers").transform;
            var result = new Transform[3];

            var spawner = Object.FindFirstObjectByType<SonTinhThuyTinh.Player.PlayerSpawner>();
            Transform start = spawner != null ? spawner.transform : root;
            float minY = sonTinh ? -1000f : 2.7f;

            Vector3 first = start.position + start.right * 4.2f + start.forward * 2.2f;
            Physics.SyncTransforms();
            if (Physics.Raycast(first + Vector3.up * 2f, Vector3.down, out RaycastHit firstHit, 8f, ~0, QueryTriggerInteraction.Ignore)) first.y = firstHit.point.y;   // the spawner floats above the ground
            result[0] = NewMark(root, 0, first, Quaternion.LookRotation(-start.right, Vector3.up));

            float[] zs = sonTinh ? new[] { 0f, 148f, 206f } : new[] { 0f, 150f, 226f };
            for (int i = 1; i < 3; i++)
            {
                float z = zs[i];
                Vector3 want = new Vector3(PathX(z, sonTinh) + 3f, 0f, z);
                Vector3 spot = FindFree(want, minY, terrain);
                Vector3 toward = new Vector3(PathX(z - 14f, sonTinh), 0f, z - 14f) - new Vector3(spot.x, 0f, spot.z);   // facing the player coming up the road
                result[i] = NewMark(root, i, spot, Quaternion.LookRotation(toward.sqrMagnitude > 0.01f ? toward : Vector3.back, Vector3.up));
            }
            return result;
        }

        static Transform NewMark(Transform root, int index, Vector3 position, Quaternion rotation)
        {
            var go = new GameObject("KeeperMark_" + (index + 1));
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(position, rotation);
            go.AddComponent<QuestKeeperMark>().giftIndex = index;
            return go.transform;
        }

        static float PathX(float z, bool sonTinh)
        {
            float t = Mathf.Clamp01(z / 50f);
            float ramp = t * t * (3f - 2f * t);
            return sonTinh
                ? ramp * (15f * Mathf.Sin(z * 0.021f) + 6f * Mathf.Sin(z * 0.058f + 1.3f))
                : ramp * (13f * Mathf.Sin(z * 0.02f + 0.6f) + 7f * Mathf.Sin(z * 0.055f + 2.1f));
        }

        // The nearest spot to `want` (spiralling out) with ground that is above `minY`, flat and not occupied by a collider.
        static Vector3 FindFree(Vector3 want, float minY, Terrain terrain)
        {
            Physics.SyncTransforms();
            for (float r = 0f; r <= 14f; r += 1.5f)
                for (int a = 0; a < 360; a += (r == 0f ? 360 : 45))
                {
                    Vector3 p = want + new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), 0f, Mathf.Sin(a * Mathf.Deg2Rad)) * r;
                    if (!Ground(p, out float y) || y < minY) continue;
                    bool flat = true;
                    foreach (Vector2 d in new[] { new Vector2(1.2f, 0f), new Vector2(-1.2f, 0f), new Vector2(0f, 1.2f), new Vector2(0f, -1.2f) })
                        if (!Ground(p + new Vector3(d.x, 0f, d.y), out float y2) || Mathf.Abs(y2 - y) > 0.5f) flat = false;
                    if (!flat) continue;
                    var feet = new Vector3(p.x, y + 0.3f, p.z);
                    bool blocked = false;
                    foreach (Collider c in Physics.OverlapCapsule(feet, feet + Vector3.up * 1.6f, 0.7f, ~0, QueryTriggerInteraction.Ignore))
                        if (!(c is TerrainCollider)) { blocked = true; break; }
                    if (blocked) continue;
                    return new Vector3(p.x, y, p.z);
                }
            Ground(want, out float fallback);
            return new Vector3(want.x, fallback, want.z);
        }

        static bool Ground(Vector3 p, out float y)
        {
            if (Physics.Raycast(new Vector3(p.x, 300f, p.z), Vector3.down, out RaycastHit hit, 600f, ~0, QueryTriggerInteraction.Ignore)) { y = hit.point.y; return true; }
            y = 0f;
            return false;
        }

        // ---------------------------------------------------------------- Map_SonTinh films

        static Film[] SonTinhFilms() => new[]
        {
            new Film
            {
                name = "MapIntro", asset = "Dialogue_Intro_SonTinh", key = "intro_SonTinh", marks = new[]
                {
                    Shot("", "Núi Tản Viên. Từ chân núi này, Sơn Tinh lên đường đi tìm sính lễ dâng lên vua Hùng.",
                        V(-60f, 48f, -42f), V(-35f, 42f, 0f), V(0f, 0f, 62f), 62f, 55f, 9f),
                    Keeper("", "Ở mỗi chặng đường đều có một Người Giữ Nhiệm Vụ. Hãy tới gần, nhấn E để nói chuyện và trả lời ba câu hỏi, họ sẽ mở khóa sính lễ kế tiếp cho ngươi.",
                        0, V(2.2f, 2.4f, 6.5f), V(1.2f, 1.9f, 4.2f), V(0f, 1.5f, 0f), 55f, 42f, 8f),
                    Shot("", "Sính lễ gồm ba linh thú. Trước hết là Ngựa chín hồng mao, ẩn trên đỉnh núi phía tây, qua con đường mòn dốc đứng.",
                        V(-185f, 60f, 118f), V(-205f, 56f, 140f), V(-256f, 3f, 182f), 58f, 46f, 9f),
                    Shot("", "Tiếp theo là Gà chín cựa, canh giữ khu rừng giữa đường lên núi.",
                        V(38f, 26f, 118f), V(24f, 24f, 140f), V(-13f, 3f, 175f), 55f, 45f, 7f),
                    Shot("", "Cuối cùng là Voi chín ngà, chúa tể của đại ngàn. Phải khuất phục nó mới đủ ba sính lễ.",
                        V(74f, 28f, 190f), V(58f, 26f, 205f), V(31.6f, 3f, 235f), 55f, 42f, 7f),
                    Shot("", "Có đủ ba linh thú, hãy qua cánh cổng ở cuối đường để về kinh đô dâng lễ lên Hùng Vương. Thủy Tinh cũng đã lên đường, đừng chậm chân!",
                        V(34f, 46f, 214f), V(20f, 44f, 236f), V(-4f, 6f, 295f), 55f, 46f, 8f),
                    Shot("Sơn Tinh", "Sính lễ này, nhất định ta phải mang về trước. Đi thôi!",
                        V(-5f, 10f, -14f), V(-2f, 7f, -9f), V(0f, 2f, 0f), 52f, 40f, 6f, true),
                }
            },
            new Film
            {
                name = "MapGift1", asset = "Dialogue_Gift1_SonTinh", key = "gift1_SonTinh", giftTrigger = 1, marks = new[]
                {
                    Keeper("", "Ngựa chín hồng mao đã theo về! Người Giữ Nhiệm Vụ thứ hai đang đứng chờ ở chặng kế tiếp, hãy tới gặp họ.",
                        1, V(2.4f, 2.4f, 7f), V(1.4f, 1.9f, 4.4f), V(0f, 1.5f, 0f), 55f, 42f, 8f, true),
                    Shot("", "Trả lời đúng ba câu hỏi của họ, rồi vào rừng tìm Gà chín cựa.",
                        V(38f, 26f, 118f), V(24f, 24f, 140f), V(-13f, 3f, 175f), 55f, 45f, 7f),
                }
            },
            new Film
            {
                name = "MapGift2", asset = "Dialogue_Gift2_SonTinh", key = "gift2_SonTinh", giftTrigger = 2, marks = new[]
                {
                    Keeper("", "Gà chín cựa đã vào tay! Người Giữ Nhiệm Vụ cuối cùng đang chờ phía trước, hãy tới nói chuyện với họ.",
                        2, V(2.4f, 2.4f, 7f), V(1.4f, 1.9f, 4.4f), V(0f, 1.5f, 0f), 55f, 42f, 8f, true),
                    Shot("", "Vượt qua ba câu hỏi, rồi hạ Voi chín ngà để đủ ba sính lễ.",
                        V(74f, 28f, 190f), V(58f, 26f, 205f), V(31.6f, 3f, 235f), 55f, 42f, 7f),
                }
            },
            new Film
            {
                name = "MapGiftsDone", asset = "Dialogue_GiftsDone_SonTinh", key = "done_SonTinh", giftTrigger = 3, marks = new[]
                {
                    Shot("", "Đã đủ ba sính lễ! Ở vùng núi này không còn việc gì phải làm nữa.",
                        V(34f, 48f, 214f), V(20f, 46f, 236f), V(-4f, 6f, 295f), 55f, 46f, 8f),
                    Shot("", "Hãy qua cánh cổng đá ở cuối con đường để về cung điện dâng lễ lên Vua Hùng.",
                        V(4f, 30f, 262f), V(-2f, 26f, 274f), V(-4f, 6f, 295f), 44f, 36f, 8f),
                }
            },
        };

        // ---------------------------------------------------------------- Map_ThuyTinh films

        static Film[] ThuyTinhFilms() => new[]
        {
            new Film
            {
                name = "MapIntro", asset = "Dialogue_Intro_ThuyTinh", key = "intro_ThuyTinh", replacesSeaIntro = "Opening", marks = new[]
                {
                    Shot("", "Vùng biển phía đông. Từ bến cảng này, Thủy Tinh lên đường đi tìm sính lễ dâng lên vua Hùng.",
                        V(-60f, 50f, -45f), V(-20f, 38f, 15f), V(70f, 3f, 110f), 62f, 55f, 9f),
                    Keeper("", "Ở mỗi chặng đường đều có một Người Giữ Nhiệm Vụ. Hãy tới gần, nhấn E để nói chuyện và trả lời ba câu hỏi, họ sẽ mở khóa sính lễ kế tiếp cho ngươi.",
                        0, V(2.2f, 2.4f, 6.5f), V(1.2f, 1.9f, 4.2f), V(0f, 1.5f, 0f), 55f, 42f, 8f),
                    Shot("", "Sính lễ gồm ba linh thú. Trước hết là Mập chín vây, hung thần của vùng đầm nước phía đông, sau bến cảng và ngọn hải đăng.",
                        V(16f, 34f, 132f), V(40f, 30f, 150f), V(78f, 1f, 192f), 55f, 45f, 8f),
                    Shot("", "Xa hơn là Sấu chín đuôi, ngự trên hòn đảo giữa vịnh. Muốn tới đó, phải qua bọn ngư tặc chắn đường.",
                        V(36f, 30f, 135f), V(22f, 26f, 152f), V(-16f, 1f, 175f), 55f, 45f, 7f),
                    Shot("", "Cuối cùng là Rùa chín chân, thủ lĩnh của loài thủy tộc, ẩn mình ở cuối vịnh.",
                        V(32f, 32f, 210f), V(18f, 28f, 230f), V(-9f, 1f, 252f), 55f, 45f, 7f),
                    Shot("", "Ngọn hải đăng bên kia vịnh đã tắt từ lâu, ma nước lảng vảng quanh đó. Hãy giúp dân chài, họ sẽ giúp lại ngươi.",
                        V(100f, 18f, 120f), V(120f, 20f, 135f), V(157f, 10f, 159f), 55f, 50f, 8f),
                    Shot("", "Có đủ ba linh thú, hãy qua cánh cổng ở cuối vịnh để dâng lễ lên Hùng Vương. Sơn Tinh cũng đã lên đường, đừng chậm chân!",
                        V(30f, 44f, 250f), V(14f, 42f, 268f), V(-1f, 6f, 295f), 55f, 48f, 8f),
                    Shot("Thủy Tinh", "Mị Nương sẽ là của ta. Đi thôi!",
                        V(-5f, 10f, -14f), V(-2f, 7f, -9f), V(0f, 2f, 0f), 52f, 40f, 6f, true),
                }
            },
            new Film
            {
                name = "MapGift1", asset = "Dialogue_Gift1_ThuyTinh", key = "gift1_ThuyTinh", giftTrigger = 1, marks = new[]
                {
                    Keeper("", "Mập chín vây đã khuất phục! Người Giữ Nhiệm Vụ thứ hai đang đứng chờ ở chặng kế tiếp, hãy tới gặp họ.",
                        1, V(2.4f, 2.4f, 7f), V(1.4f, 1.9f, 4.4f), V(0f, 1.5f, 0f), 55f, 42f, 8f, true),
                    Shot("", "Trả lời đúng ba câu hỏi của họ, rồi ra hòn đảo giữa vịnh tìm Sấu chín đuôi.",
                        V(36f, 30f, 135f), V(22f, 26f, 152f), V(-16f, 1f, 175f), 55f, 45f, 7f),
                }
            },
            new Film
            {
                name = "MapGift2", asset = "Dialogue_Gift2_ThuyTinh", key = "gift2_ThuyTinh", giftTrigger = 2, marks = new[]
                {
                    Keeper("", "Sấu chín đuôi đã quy phục! Người Giữ Nhiệm Vụ cuối cùng đang chờ phía trước, hãy tới nói chuyện với họ.",
                        2, V(2.4f, 2.4f, 7f), V(1.4f, 1.9f, 4.4f), V(0f, 1.5f, 0f), 55f, 42f, 8f, true),
                    Shot("", "Vượt qua ba câu hỏi, rồi xuống cuối vịnh thu phục Rùa chín chân.",
                        V(32f, 32f, 210f), V(18f, 28f, 230f), V(-9f, 1f, 252f), 55f, 45f, 7f),
                }
            },
            new Film
            {
                name = "MapGiftsDone", asset = "Dialogue_GiftsDone_ThuyTinh", key = "done_ThuyTinh", giftTrigger = 3, marks = new[]
                {
                    Shot("", "Đã đủ ba sính lễ! Ở vùng biển này không còn việc gì phải làm nữa.",
                        V(30f, 44f, 235f), V(18f, 42f, 255f), V(-1f, 6f, 295f), 55f, 48f, 8f),
                    Shot("", "Hãy qua cánh cổng đá ở cuối vịnh để về cung điện dâng lễ lên Vua Hùng.",
                        V(14f, 20f, 268f), V(5f, 15f, 281f), V(-1f, 10f, 295f), 52f, 44f, 8f),
                }
            },
        };

        // ---------------------------------------------------------------- Map_HungVuong and Map_FinalBattle

        static Film PalaceFilm() => new Film
        {
            name = "MapPalace", asset = "Dialogue_Palace_Arrival", key = "palace", marks = new[]
            {
                Shot("", "Kinh đô Văn Lang. Sau bao gian nan, ngươi đã mang đủ ba sính lễ về đến chân cung điện Vua Hùng.",
                    V(-100f, 40f, -50f), V(-80f, 34f, -28f), V(0f, 18f, 32f), 62f, 54f, 9f),
                Shot("", "Hành trình lấy sính lễ đã xong. Chỉ còn một việc: dâng lễ lên Vua Hùng để ngài phán xét.",
                    V(-45f, 40f, -10f), V(-30f, 36f, 8f), V(0f, 30f, 62f), 58f, 48f, 8f),
                Shot("", "Hãy đi thẳng theo con đường lát đá giữa hai hàng đuốc, qua sân điện rồi bước vào chính điện.",
                    V(-66f, 9f, -9f), V(-52f, 8f, -2f), V(0f, 3f, 31f), 55f, 46f, 8f),
                Shot("", "Ở trong kia, Vua Hùng và Mị Nương đang đợi. Đối thủ của ngươi cũng đã có mặt.",
                    V(-22f, 12f, 12f), V(-12f, 9f, 20f), V(0f, 3f, 33f), 55f, 44f, 7f),
            }
        };

        static Film FinalFilm() => new Film
        {
            name = "MapFinalIntro", asset = "Dialogue_FinalBattle_Intro", key = "final", marks = new[]
            {
                Shot("", "Thung lũng cuối cùng, nơi núi và biển giáp nhau. Trận đấu này sẽ quyết định ai được cưới Mị Nương.",
                    V(0f, 9f, -36f), V(0f, 6f, -27f), V(0f, 10f, 40f), 62f, 54f, 9f),
                Shot("", "Bên trái, tượng Sơn Tinh bằng đá, chúa tể của non cao.",
                    V(14f, 5f, 8f), V(9f, 6f, 12f), V(-19f, 16f, 34f), 55f, 48f, 7f),
                Shot("", "Bên phải, tượng Thủy Tinh phủ lông vũ, chúa tể của biển cả.",
                    V(-14f, 5f, 8f), V(-9f, 6f, 12f), V(19f, 16f, 34f), 55f, 48f, 7f),
                Shot("", "Kẻ đứng trước mặt ngươi là đối thủ. Dùng phím J để đánh thường, chuột phải để đánh mạnh, và ba chiêu thức U, I, O để áp đảo hắn.",
                    V(3.2f, 4f, -20f), V(3.2f, 3f, -15f), V(0f, 1.8f, 6f), 55f, 46f, 9f),
                Shot("", "Khi thanh máu của hắn còn một nửa, hắn sẽ cuồng nộ. Hãy cẩn thận. Chiến đấu thôi!",
                    V(6f, 3f, -4f), V(3f, 2.4f, 1f), V(0f, 2f, 8f), 52f, 42f, 7f),
            }
        };

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        static Mark Shot(string speaker, string line, Vector3 from, Vector3 to, Vector3 look, float fovFrom, float fovTo, float seconds, bool clearView = false) =>
            new Mark { speaker = speaker, line = line, from = from, to = to, look = look, fovFrom = fovFrom, fovTo = fovTo, seconds = seconds, clearView = clearView, keeper = -1 };

        static Mark Keeper(string speaker, string line, int keeper, Vector3 from, Vector3 to, Vector3 look, float fovFrom, float fovTo, float seconds, bool clearView = true) =>
            new Mark { speaker = speaker, line = line, from = from, to = to, look = look, fovFrom = fovFrom, fovTo = fovTo, seconds = seconds, clearView = clearView, keeper = keeper };

        // ---------------------------------------------------------------- build

        static void BuildFilms(Film[] films, float minGround, Transform[] keepers)
        {
            var runner = Object.FindFirstObjectByType<DialogueRunner>(FindObjectsInactive.Include);
            if (runner == null)
            {
                // Map_HungVuong and Map_FinalBattle had no dialogue box: take the Prologue's, as the other maps did
                runner = HorseQuestBuilder.CopyDialogueUi(EditorSceneManager.GetActiveScene());
                var runnerSo = new SerializedObject(runner);
                runnerSo.FindProperty("keepIllustration").boolValue = false;
                runnerSo.ApplyModifiedPropertiesWithoutUndo();
            }
            Terrain terrain = Terrain.activeTerrain;

            foreach (string old in new[] { "MapIntro", "MapFilms" })
            {
                GameObject o = GameObject.Find(old);
                if (o != null) Object.DestroyImmediate(o);
            }
            var filmsRoot = new GameObject("MapFilms").transform;

            foreach (Film film in films)
            {
                var root = new GameObject(film.name);
                root.transform.SetParent(filmsRoot, false);
                var marksRoot = new GameObject("Marks").transform;
                marksRoot.SetParent(root.transform, false);

                var lines = new List<DialogueLine>();
                var shots = new List<MapIntroCutscene.Shot>();
                for (int i = 0; i < film.marks.Length; i++)
                {
                    Mark m = film.marks[i];
                    lines.Add(new DialogueLine { speaker = m.speaker, text = m.line });
                    Transform keeper = m.keeper >= 0 && keepers != null && m.keeper < keepers.Length ? keepers[m.keeper] : null;
                    shots.Add(new MapIntroCutscene.Shot
                    {
                        from = Place(marksRoot, "Shot" + (i + 1) + "_from", m.from, terrain, minGround, keeper),
                        to = Place(marksRoot, "Shot" + (i + 1) + "_to", m.to, terrain, minGround, keeper),
                        look = Place(marksRoot, "Shot" + (i + 1) + "_look", m.look, terrain, minGround, keeper),
                        fovFrom = m.fovFrom, fovTo = m.fovTo, seconds = m.seconds, clearView = m.clearView,
                    });
                }

                DialogueSequence sequence = SaveSequence(film.asset, lines);
                var cutscene = root.AddComponent<MapIntroCutscene>();
                var so = new SerializedObject(cutscene);
                so.FindProperty("key").stringValue = film.key;
                so.FindProperty("runner").objectReferenceValue = runner;
                so.FindProperty("sequence").objectReferenceValue = sequence;
                so.FindProperty("replacesSeaIntro").stringValue = film.replacesSeaIntro ?? "";
                so.FindProperty("giftCountTrigger").intValue = film.giftTrigger;
                SerializedProperty arr = so.FindProperty("shots");
                arr.arraySize = shots.Count;
                for (int i = 0; i < shots.Count; i++)
                {
                    SerializedProperty e = arr.GetArrayElementAtIndex(i);
                    e.FindPropertyRelative("from").objectReferenceValue = shots[i].from;
                    e.FindPropertyRelative("to").objectReferenceValue = shots[i].to;
                    e.FindPropertyRelative("look").objectReferenceValue = shots[i].look;
                    e.FindPropertyRelative("fovFrom").floatValue = shots[i].fovFrom;
                    e.FindPropertyRelative("fovTo").floatValue = shots[i].fovTo;
                    e.FindPropertyRelative("seconds").floatValue = shots[i].seconds;
                    e.FindPropertyRelative("clearView").boolValue = shots[i].clearView;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[MapIntro] built " + films.Length + " film(s) in " + EditorSceneManager.GetActiveScene().name);
        }

        static Transform Place(Transform parent, string name, Vector3 p, Terrain terrain, float minGround, Transform keeper)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            if (keeper != null)
            {
                go.transform.position = keeper.position + keeper.rotation * p;
                return go.transform;
            }
            float ground = terrain != null ? terrain.SampleHeight(new Vector3(p.x, 0f, p.z)) + terrain.transform.position.y : 0f;
            go.transform.position = new Vector3(p.x, Mathf.Max(ground, minGround) + p.y, p.z);
            return go.transform;
        }

        static DialogueSequence SaveSequence(string name, List<DialogueLine> lines)
        {
            string path = "Assets/Data/Dialogue/" + name + ".asset";
            var seq = AssetDatabase.LoadAssetAtPath<DialogueSequence>(path);
            if (seq == null)
            {
                seq = ScriptableObject.CreateInstance<DialogueSequence>();
                AssetDatabase.CreateAsset(seq, path);
            }
            var so = new SerializedObject(seq);
            SerializedProperty arr = so.FindProperty("lines");
            arr.arraySize = lines.Count;
            for (int i = 0; i < lines.Count; i++)
            {
                SerializedProperty e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("speaker").stringValue = lines[i].speaker;
                e.FindPropertyRelative("text").stringValue = lines[i].text;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(seq);
            AssetDatabase.SaveAssets();
            return seq;
        }
    }
}
