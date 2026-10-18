using System.Collections.Generic;
using System.IO;
using SonTinhThuyTinh.Combat;
using SonTinhThuyTinh.Dialogue;
using SonTinhThuyTinh.Quest;
using UnityEditor;
using UnityEngine;
using static SonTinhThuyTinh.EditorTools.SonTinhMapBuilder;

namespace SonTinhThuyTinh.EditorTools
{
    // The story part of Map_ThuyTinh's events (docs/ke-hoach-thuy-tinh-su-kien.md), the pieces that are not a boss arena:
    //  - the harbour: the old fisherman (placeholder model), the pirates on the deck (NinjaEnemy), the chain across the pier;
    //  - the village elder on the fishing village's island;
    //  - the lighthouse: the water ghosts at its foot, the lamp (E), the beacon, the mist wall across Shark Bay's entrance;
    //  - short narrations: the opening at the start, the crocodile's island.
    public static partial class ThuyTinhMapBuilder
    {
        const string NpcPrefab = "Assets/Prefabs/Characters/SonTinh.prefab";          // the old Sơn Tinh model, a placeholder for the villagers
        const string FoePrefab = "Assets/Prefabs/Characters/Ninja_Enemy.prefab";      // pirates and ghosts until they get models of their own

        static void BuildStoryEvents(Transform events, DialogueRunner runner)
        {
            var foe = AssetDatabase.LoadAssetAtPath<GameObject>(FoePrefab);
            var npc = AssetDatabase.LoadAssetAtPath<GameObject>(NpcPrefab);
            if (foe == null || npc == null) { Debug.LogError("Story events: missing " + (foe == null ? FoePrefab : NpcPrefab)); return; }
            BuildHarbourEvent(Group(events, "HarbourEvent"), runner, foe, npc);
            BuildElder(Group(events, "VillageElder"), runner, npc);
            BuildLighthouseEvent(Group(events, "LighthouseEvent"), runner, foe);
            BuildNarrations(Group(events, "Narrations"), runner);
        }

        // ---------------------------------------------------------------- helpers

        // A talker: the placeholder model with a straw hat, a capsule, an Interactable ("Nhấn E ...") and a SeaNpc.
        static SeaNpc MakeNpc(Transform parent, GameObject prefab, string name, SeaNpc.Role role, Vector3 at, Vector3 lookTowards, Color hat, DialogueRunner runner,
            DialogueSequence offer, DialogueSequence reminder, DialogueSequence thanks, DialogueSequence afterwards, bool wearHat = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, go.transform);
            model.transform.localPosition = Vector3.zero;
            Vector3 look = lookTowards - at; look.y = 0f;
            model.transform.rotation = Quaternion.LookRotation(look.sqrMagnitude > 0.01f ? look : Vector3.forward);
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.95f, 0f); capsule.height = 1.9f; capsule.radius = 0.35f;
            // the straw hat (a flat disc over a low cone made of a squashed sphere): the cheap sign that this is a fisher
            if (wearHat && !NewProps.DressHat(go.transform))
            {
            MapDecor.Part(PrimitiveType.Cylinder, "HatBrim", go.transform, new Vector3(0f, 1.82f, 0f), new Vector3(0.78f, 0.02f, 0.78f), Quaternion.identity, MapDecor.Lit("Mat_Hat_" + ColorUtility.ToHtmlStringRGB(hat), hat, 0f, 0.1f, true));
            MapDecor.Part(PrimitiveType.Sphere, "HatTop", go.transform, new Vector3(0f, 1.86f, 0f), new Vector3(0.42f, 0.26f, 0.42f), Quaternion.identity, MapDecor.Lit("Mat_Hat_" + ColorUtility.ToHtmlStringRGB(hat), hat, 0f, 0.1f, true));
            }

            var talk = go.AddComponent<Interactable>();
            var tso = new SerializedObject(talk);
            tso.FindProperty("prompt").stringValue = "Nhấn E để nói chuyện";
            tso.FindProperty("radius").floatValue = 3f;
            tso.ApplyModifiedPropertiesWithoutUndo();

            var script = go.AddComponent<SeaNpc>();
            var so = new SerializedObject(script);
            so.FindProperty("role").enumValueIndex = (int)role;
            so.FindProperty("runner").objectReferenceValue = runner;
            so.FindProperty("offer").objectReferenceValue = offer;
            so.FindProperty("reminder").objectReferenceValue = reminder;
            so.FindProperty("thanks").objectReferenceValue = thanks;
            so.FindProperty("afterwards").objectReferenceValue = afterwards;
            so.FindProperty("body").objectReferenceValue = model.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
            return script;
        }

        // A band of fighters (NinjaEnemy) at the given spots facing `facing`, plus the SeaFoeDirector that runs the fight.
        static SeaFoeDirector MakeBand(Transform parent, GameObject prefab, string foeName, SeaFoeDirector.Kind kind, IList<Vector3> spots, Vector3 facing, Vector3 centre,
            float radius, float trigger, Transform playerMark, GameObject[] blockers, DialogueRunner runner, DialogueSequence ambush, bool ghostGlow)
        {
            var marks = new GameObject("Spots").transform;
            marks.SetParent(parent, false);
            var spotMarks = new Transform[spots.Count];
            var foes = new NinjaEnemy[spots.Count];
            for (int i = 0; i < spots.Count; i++)
            {
                spotMarks[i] = Mark(marks, "Spot" + (i + 1), spots[i], facing);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                go.name = foeName + "_" + (i + 1);
                go.transform.SetPositionAndRotation(spots[i], spotMarks[i].rotation);
                foes[i] = go.GetComponent<NinjaEnemy>();
                if (ghostGlow)
                {
                    var light = new GameObject("GhostGlow").AddComponent<Light>();
                    light.transform.SetParent(go.transform, false);
                    light.transform.localPosition = Vector3.up * 1.3f;
                    light.type = LightType.Point; light.color = new Color(0.4f, 0.9f, 1f); light.range = 6f; light.intensity = 3f; light.shadows = LightShadows.None;
                }
            }
            var centreMark = Mark(parent, "Centre", centre, facing);
            var director = parent.gameObject.AddComponent<SeaFoeDirector>();
            var so = new SerializedObject(director);
            so.FindProperty("kind").enumValueIndex = (int)kind;
            var list = so.FindProperty("foes"); list.arraySize = foes.Length;
            for (int i = 0; i < foes.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = foes[i];
            list = so.FindProperty("spots"); list.arraySize = spotMarks.Length;
            for (int i = 0; i < spotMarks.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = spotMarks[i];
            so.FindProperty("centre").objectReferenceValue = centreMark;
            so.FindProperty("radius").floatValue = radius;
            so.FindProperty("triggerRadius").floatValue = trigger;
            so.FindProperty("playerMark").objectReferenceValue = playerMark;
            list = so.FindProperty("blockers"); list.arraySize = blockers.Length;
            for (int i = 0; i < blockers.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = blockers[i];
            so.FindProperty("runner").objectReferenceValue = runner;
            so.FindProperty("ambush").objectReferenceValue = ambush;
            so.ApplyModifiedPropertiesWithoutUndo();
            return director;
        }

        // ---------------------------------------------------------------- the harbour: fisherman, pirates, chain

        static void BuildHarbourEvent(Transform ev, DialogueRunner runner, GameObject foe, GameObject npc)
        {
            Vector2 h = HarbourCentre;
            Vector2 start = SeaSegments[0];   // where the pier leaves the causeway

            // the fisherman on the causeway just before the pier
            Vector2 fisher = new(PathX(101f) + 1.8f, 101f);
            var offer = SaveDialogue("Fisherman_Offer",
                ("Lão chài", "Này, chàng trai áo xanh! Dáng đi, thần sắc thế kia... có phải Thủy Tinh, chúa vùng nước đây không?"),
                (Hero, "Chính là ta. Ta đang đi tìm ba linh thú của vùng nước làm sính lễ dâng Vua Hùng, để cầu hôn Mị Nương."),
                ("Lão chài", "Ôi, ngài đến đúng lúc mà cũng đến trễ! Mấy tháng nay bọn ngư tặc chiếm Bến Thuyền, đốt thuyền, xé lưới, cướp cả viên ngọc làm đèn cho hải đăng."),
                ("Lão chài", "Hải đăng tắt thì sương cứ dày lên mỗi ngày. Mập Chín Vây, Sấu Chín Đuôi, Rùa Chín Chân đều nổi giận, tưởng chính dân chài đã phản bội họ."),
                (Hero, "Vậy ta sẽ dẹp bọn ngư tặc trước."),
                ("Lão chài", "Chúng canh ngay trên sàn bến phía trước kia. Dây xích chắn đường sang làng chỉ gỡ được khi bọn chúng gục. Cầu mong thần thủy phù hộ ngài!"),
                ("", "Nhiệm vụ: dẹp bọn ngư tặc ở Bến Thuyền, mở dây xích chắn đường tới Làng Chài. Sau đó lên Hải đăng thắp lại ngọn đèn, rồi đi thuần phục ba linh thú."));
            var reminder = SaveDialogue("Fisherman_Reminder",
                ("Lão chài", "Bọn ngư tặc ở ngay sàn bến phía trước. Chúng đông và hung hăng lắm, cẩn thận! Dẹp xong thì dây xích tự gỡ."));
            var thanks = SaveDialogue("Fisherman_Thanks",
                (Hero, "Bọn ngư tặc đã bị dẹp, dây xích chắn bến cũng gỡ rồi."),
                ("Lão chài", "Trời đất! Ngài đúng là chúa vùng nước. Giờ ngài sang Làng Chài gặp trưởng làng, rồi ra hải đăng thắp lại đèn. Có đèn thì sương mới tan."),
                ("Lão chài", "Nhớ đi trên cầu ván và chỗ nước nông thôi nhé. Chỗ nước sâu dòng chảy cuốn người đi như chơi."));
            var after = SaveDialogue("Fisherman_After",
                ("Lão chài", "Đi đường sông nước nhớ giữ mình. Chỉ bước trên cầu ván và chỗ nước nông, nước sâu là dòng chảy cuốn đi đấy!"));
            Vector3 fisherAt = Ground(fisher.x, fisher.y);
            MakeNpc(ev, npc, "Fisherman", SeaNpc.Role.Fisherman, fisherAt, fisherAt + Vector3.back * 5f, new Color(0.78f, 0.66f, 0.30f), runner, offer, reminder, thanks, after);

            // the chain across the pier just beyond the deck, and the pirates on the deck
            var chain = BuildBarrier(ev, "HarbourChain", h + new Vector2(7.6f, 0f), Vector2.right, 26f, 4f, new Color(0.22f, 0.22f, 0.25f));
            var playerMark = Mark(ev, "PiratePlayerMark", new Vector3(start.x + 4f, PierDeck + 0.1f, start.y), Vector3.right);
            float y = PierDeck + 0.05f;
            var spots = new[]
            {
                new Vector3(h.x + 2.5f, y, h.y - 3.2f), new Vector3(h.x + 3.6f, y, h.y + 0.4f),
                new Vector3(h.x + 2.5f, y, h.y + 3.8f), new Vector3(h.x + 1.6f, y, h.y - 5.0f)   /* (was on the bridge start beside the chain) */,
            };
            var ambush = SaveDialogue("Pirates_Ambush",
                ("Ngư tặc", "Dừng lại! Bến này là của bọn ta. Tên áo xanh kia, muốn qua thì để lại hết đồ quý."),
                (Hero, "Bến này là của dân chài. Tránh đường, ta chỉ nói một lần."),
                ("Ngư tặc", "Cứng đầu à? Anh em, xử hắn rồi quăng xuống nước!"));
            MakeBand(Group(ev, "Pirates"), foe, "Pirate", SeaFoeDirector.Kind.Pirates, spots, Vector3.left, new Vector3(h.x + 3f, y, h.y), 9f, 17f, playerMark, new[] { chain }, runner, ambush, false);
        }

        // ---------------------------------------------------------------- the village elder

        static void BuildElder(Transform ev, DialogueRunner runner, GameObject npc)
        {
            Vector2 c = SeaIslands[0].Centre;
            var offer = SaveDialogue("Elder_Offer",
                ("Trưởng làng", "Dân làng chài xin chào chúa vùng nước. Lão chài ngoài bến đã nhắn tin rồi: ngài đã dẹp ngư tặc giúp chúng tôi."),
                ("Trưởng làng", "Ngọn hải đăng trên đồi phía đông là con mắt của vịnh này. Từ khi viên ngọc bị cướp, đèn tắt, ma nước từ sương kéo về chiếm chân tháp."),
                (Hero, "Ta sẽ lên đó, đuổi ma nước và thắp lại đèn."),
                ("Trưởng làng", "Đèn sáng thì sương tan, lối vào Vịnh Mập mới hiện ra. Mập Chín Vây ở trong đó, nó đang giận lắm. Còn Sấu Chín Đuôi giữ ngọc dưới đáy hồ phía tây, Rùa Chín Chân ngự ở đền chìm cuối đường đắp."),
                ("", "Nhiệm vụ: lên Hải đăng ở phía đông làng, đuổi ma nước và thắp đèn. Sau đó vào Vịnh Mập, rồi tới đảo Sấu và đền chìm."));
            var reminder = SaveDialogue("Elder_Reminder",
                ("Trưởng làng", "Đồi hải đăng ở phía đông làng, cứ theo cầu ván mà đi. Ma nước đứng ở chân tháp, đánh tan chúng mới thắp được đèn."));
            var thanks = SaveDialogue("Elder_Thanks",
                ("Trưởng làng", "Hải đăng sáng rồi! Sương đang tan... Ngài đúng là chúa vùng nước. Giờ vào Vịnh Mập đi, rồi tới đảo Sấu ở phía tây."));
            Vector3 at = Ground(c.x + 2.4f, c.y + 1.0f);
            MakeNpc(ev, npc, "VillageElder", SeaNpc.Role.Elder, at, Ground(HarbourCentre.x, HarbourCentre.y), new Color(0.55f, 0.72f, 0.80f), runner, offer, reminder, thanks, null, wearHat: false);
        }

        // ---------------------------------------------------------------- the lighthouse

        const string MistMaterialPath = DataDir + "/Mat_Mist.mat";

        static Material MistMaterial()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(MistMaterialPath);
            if (m == null)
            {
                Directory.CreateDirectory(DataDir);
                m = new Material(Shader.Find("Sprites/Default"));
                AssetDatabase.CreateAsset(m, MistMaterialPath);
            }
            m.color = new Color(0.84f, 0.93f, 0.97f, 0.30f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void BuildLighthouseEvent(Transform ev, DialogueRunner runner, GameObject foe)
        {
            SeaIsland isl = SeaIslands[1];
            var towerGo = GameObject.Find("Generated/Sea/Lighthouse/LighthouseTower");
            if (towerGo == null) { Debug.LogError("Story events: no lighthouse tower in the scene."); return; }
            Vector3 tower = towerGo.transform.position;
            Vector2 landing = Landing(isl, SeaIslands[0].Centre);                 // where the pier from the village arrives
            Vector2 dirIn = (landing - isl.Centre).normalized;
            Vector3 toward = new(dirIn.x, 0f, dirIn.y);
            Quaternion q1 = Quaternion.Euler(0f, 55f, 0f), q2 = Quaternion.Euler(0f, -55f, 0f);
            Vector3[] spots =
            {
                OnHill(tower + q1 * toward * 7.2f), OnHill(tower + q2 * toward * 7.2f), OnHill(tower + toward * 10f),
            };
            var playerMark = Mark(ev, "GhostPlayerMark", new Vector3(landing.x, PierDeck + 0.1f, landing.y), -toward);
            var ambush = SaveDialogue("Ghosts_Ambush",
                ("Ma nước", "Ánh sáng... chúng ta ghét ánh sáng... Đừng chạm vào ngọn đèn!"),
                (Hero, "Ngọn đèn này soi đường cho dân chài. Tan đi!"));
            // they wake a few steps up the hill from where the pier lands (that is about 25 m from the tower)
            float trigger = Vector2.Distance(landing, isl.Centre) - 5f;
            MakeBand(Group(ev, "Ghosts"), foe, "WaterGhost", SeaFoeDirector.Kind.Ghosts, spots, toward, tower, 8f, trigger, playerMark, new GameObject[0], runner, ambush, true);

            // the lamp: E at the foot of the tower
            var lampSpot = new GameObject("LampSpot").transform;
            lampSpot.SetParent(towerGo.transform, false);
            lampSpot.localPosition = Vector3.zero;
            var lamp = lampSpot.gameObject.AddComponent<Interactable>();
            var lso = new SerializedObject(lamp);
            lso.FindProperty("prompt").stringValue = "Nhấn E để thắp đèn hải đăng";
            lso.FindProperty("radius").floatValue = 6f;
            lso.ApplyModifiedPropertiesWithoutUndo();

            // the mist wall across Shark Bay's entrance (a box collider and a few layers of soft mist), gone when the lamp is lit
            Vector2 mp = PoolCentre + PoolEntranceDir * (PoolRingRadius + 0.8f);
            var mist = new GameObject("SharkMist").transform;
            mist.SetParent(ev, false);
            mist.SetPositionAndRotation(new Vector3(mp.x, WaterY - 0.5f, mp.y), Quaternion.LookRotation(new Vector3(PoolEntranceDir.x, 0f, PoolEntranceDir.y)));
            var box = mist.gameObject.AddComponent<BoxCollider>();
            box.size = new Vector3(17f, 14f, 1.2f);
            box.center = new Vector3(0f, 6f, 0f);
            Material mat = MistMaterial();
            for (int i = 0; i < 5; i++)
            {
                var quad = MapDecor.Part(PrimitiveType.Quad, "Mist" + i, mist, new Vector3((i - 2) * 0.6f, 5.5f + (i % 2) * 0.8f, (i - 2) * 0.5f), new Vector3(17f, 11f, 1f), Quaternion.Euler(0f, 0f, (i - 2) * 2f), mat);
                var r = quad.GetComponent<MeshRenderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }

            var lampText = SaveDialogue("Lamp_Light",
                ("", "Thủy Tinh đặt tay lên chiếc đèn nguội lạnh. Không còn viên ngọc, chàng dồn thủy lực của chính mình vào ngọn đèn."),
                ("", "Ngọn lửa xanh bùng lên, tia sáng quét qua mặt vịnh. Sương mù dày đặc bắt đầu tan, để lộ lối vào Vịnh Mập ở phía tây."),
                (Hero, "Giờ đến lượt Mập Chín Vây."));

            var beaconLight = towerGo.transform.Find("Beacon").GetComponent<Light>();
            var beacon = ev.gameObject.AddComponent<SeaLampBeacon>();
            var so = new SerializedObject(beacon);
            so.FindProperty("lamp").objectReferenceValue = lamp;
            so.FindProperty("beacon").objectReferenceValue = beaconLight;
            so.FindProperty("mist").objectReferenceValue = mist.gameObject;
            so.FindProperty("mistCentre").objectReferenceValue = mist;
            so.FindProperty("runner").objectReferenceValue = runner;
            so.FindProperty("lighting").objectReferenceValue = lampText;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Vector3 OnHill(Vector3 p) => new(p.x, GroundHeight(p.x, p.z) + 0.05f, p.z);

        // ---------------------------------------------------------------- narrations

        static void BuildNarrations(Transform parent, DialogueRunner runner)
        {
            void Intro(string name, string key, Vector3 at, float radius, DialogueSequence sequence)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.transform.position = at;
                var t = go.AddComponent<SeaIntroTrigger>();
                var so = new SerializedObject(t);
                so.FindProperty("runner").objectReferenceValue = runner;
                so.FindProperty("sequence").objectReferenceValue = sequence;
                so.FindProperty("key").stringValue = key;
                so.FindProperty("radius").floatValue = radius;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            Intro("Opening", "Opening", Ground(PathX(0f), 0f), 9f, SaveDialogue("Opening",
                ("", "Sương phủ kín vùng đầm. Từ xa, một ngọn hải đăng tắt lịm đứng lặng trên đồi, không còn tia sáng nào."),
                (Hero, "Nước đang dậy khác thường. Hãy theo đường đắp tới Bến Thuyền, hỏi thăm người dân chài.")));
            Vector2 s1 = Stop(1);
            Intro("CrocodileIsland", "Crocodile", Ground(s1.x, s1.y), 28f, SaveDialogue("Crocodile_Intro",
                ("", "Giữa hồ đen, một cồn đất tròn nhô lên khỏi mặt nước. Mặt hồ lặng ngắt, nhưng dưới đáy sâu có thứ gì đó đang uốn mình."),
                (Hero, "Viên minh châu sáng dưới kia... Sấu Chín Đuôi đang canh giữ nó. Ta cảm nhận được chín cái đuôi cựa quậy.")));
        }
    }
}
